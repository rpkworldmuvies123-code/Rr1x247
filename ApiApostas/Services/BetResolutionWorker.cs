using System.Text;
using System.Text.Json;
using ApiApostas.Controllers;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace ApiApostas.Services;

public sealed class BetResolutionWorker(
    RabbitMqService rabbit,
    KafkaProducerService kafka,
    IServiceScopeFactory scopeFactory,
    ILogger<BetResolutionWorker> logger) : BackgroundService
{
    private const int MaxRetries = 3;

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Prefetch 1: só busca a próxima mensagem depois de processar a atual
        await rabbit.Channel.BasicQosAsync(prefetchSize: 0, prefetchCount: 1, global: false, cancellationToken: stoppingToken);

        var consumer = new AsyncEventingBasicConsumer(rabbit.Channel);
        consumer.ReceivedAsync += (_, ea) => HandleMessageAsync(ea, stoppingToken);

        await rabbit.Channel.BasicConsumeAsync(
            queue: RabbitMqService.QueueBetResolution,
            autoAck: false,
            consumer: consumer,
            cancellationToken: stoppingToken);

        logger.LogInformation("BetResolutionWorker a escutar fila '{Queue}'", RabbitMqService.QueueBetResolution);

        await Task.Delay(Timeout.Infinite, stoppingToken).ConfigureAwait(false);
    }

    private async Task HandleMessageAsync(BasicDeliverEventArgs ea, CancellationToken ct)
    {
        var body = Encoding.UTF8.GetString(ea.Body.Span);
        string? codigoJogo = null;

        try
        {
            var msg = JsonSerializer.Deserialize<BetResolutionMessage>(body,
                new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

            if (msg is null) throw new InvalidOperationException("Payload inválido.");
            codigoJogo = msg.CodigoJogo;

            await using var scope = scopeFactory.CreateAsyncScope();
            var jogosController = scope.ServiceProvider.GetRequiredService<JogosController>();
            var apostasResolvidas = await jogosController.ProcessarPagamentosPendentesAsync(codigoJogo);

            foreach (var a in apostasResolvidas)
                kafka.Publicar("bet-resolved", codigoJogo,
                    a.UtilizadorId, a.TipoAposta, a.Montante, a.Odd, a.Estado, a.ApostaId);

            await rabbit.Channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);

            logger.LogInformation("[RabbitMQ] Apostas resolvidas para {CodigoJogo}", codigoJogo);
        }
        catch (Exception ex)
        {
            var retryCount = GetRetryCount(ea);
            logger.LogError(ex, "[RabbitMQ] Falha ao resolver apostas para {CodigoJogo} (tentativa {N}/{Max})",
                codigoJogo ?? "?", retryCount + 1, MaxRetries);

            if (retryCount < MaxRetries)
            {
                // Publicar nova mensagem com contador incrementado, ack a original.
                // BasicNack(requeue:true) não permite alterar headers — por isso re-publicamos manualmente.
                var props = new BasicProperties();
                props.Headers = new Dictionary<string, object?> { ["x-retry-count"] = (long)(retryCount + 1) };
                props.DeliveryMode = DeliveryModes.Persistent;
                await rabbit.Channel.BasicPublishAsync(
                    exchange: "",
                    routingKey: RabbitMqService.QueueBetResolution,
                    mandatory: false,
                    basicProperties: props,
                    body: ea.Body,
                    cancellationToken: ct);
                await rabbit.Channel.BasicAckAsync(ea.DeliveryTag, multiple: false, cancellationToken: ct);
            }
            else
            {
                // Esgotou retries → DLQ via x-dead-letter
                logger.LogWarning("[RabbitMQ] Mensagem enviada para DLQ: {CodigoJogo}", codigoJogo ?? "?");
                await rabbit.Channel.BasicNackAsync(ea.DeliveryTag, multiple: false, requeue: false, cancellationToken: ct);
            }
        }
    }

    private static int GetRetryCount(BasicDeliverEventArgs ea)
    {
        if (ea.BasicProperties.Headers is not null &&
            ea.BasicProperties.Headers.TryGetValue("x-retry-count", out var val))
        {
            return Convert.ToInt32(val);
        }
        return 0;
    }

    private sealed record BetResolutionMessage(string CodigoJogo, int EstadoJogo);
}
