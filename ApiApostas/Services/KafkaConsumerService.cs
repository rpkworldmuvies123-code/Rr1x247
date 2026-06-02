using System.Text.Json;
using Confluent.Kafka;

namespace ApiApostas.Services;

public sealed class KafkaConsumerService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    RabbitMqService rabbit,
    ILogger<KafkaConsumerService> logger) : BackgroundService
{
    private const string TopicGameEvents = "game-events";
    private const string ConsumerGroup = "apiapostas-jogos";

    protected override Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Corre num thread dedicado para não bloquear o startup
        return Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);
    }

    private void ConsumeLoop(CancellationToken ct)
    {
        var bootstrapServers = configuration["Kafka:BootstrapServers"] ?? "localhost:9092";

        var config = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = ConsumerGroup,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(config).Build();
        consumer.Subscribe(TopicGameEvents);

        logger.LogInformation("Kafka consumer iniciado — topic: {Topic}, group: {Group}", TopicGameEvents, ConsumerGroup);

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(ct);
                if (result?.Message?.Value is null) continue;

                ProcessarEvento(result.Message.Value).GetAwaiter().GetResult();
                consumer.Commit(result);
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                // Tópico ainda não existe — aguarda até a primeira mensagem ser publicada
                logger.LogDebug("[Kafka] Topic {Topic} ainda não existe, a aguardar...", TopicGameEvents);
                Thread.Sleep(5000);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao processar mensagem Kafka");
                Thread.Sleep(2000);
            }
        }

        consumer.Close();
    }

    private async Task ProcessarEvento(string payload)
    {
        var evento = JsonSerializer.Deserialize<JogoEventoDto>(payload,
            new JsonSerializerOptions { PropertyNameCaseInsensitive = true });

        if (evento is null) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        try
        {
            var locais = await db.ConsultarJogosAsync();
            var local = locais.FirstOrDefault(j => (string)j["CodigoJogo"]! == evento.CodigoJogo);

            if (local is null)
            {
                await db.InserirJogoAsync(
                    evento.CodigoJogo, evento.DataHoraInicio,
                    evento.EquipaCasa, evento.EquipaFora, evento.TipoCompeticao);

                logger.LogInformation("[Kafka] Jogo inserido: {CodigoJogo}", evento.CodigoJogo);
                return;
            }

            var estadoLocal = Convert.ToInt32(local["Estado"]);
            if (estadoLocal is 3 or 4 or 5)
            {
                // Jogo já terminal — se houver pagamentos pendentes significa que a publicação
                // RabbitMQ falhou numa tentativa anterior (ex: RabbitMQ estava em baixo).
                // Re-publicar para garantir resolução das apostas.
                var pendentes = await db.ObterPagamentosPendentesAsync(evento.CodigoJogo);
                if (pendentes.Count > 0)
                {
                    await rabbit.PublicarResolucaoApostasAsync(evento.CodigoJogo, estadoLocal);
                    logger.LogWarning(
                        "[Kafka] Re-publicado RabbitMQ para {CodigoJogo} — {N} pagamento(s) pendente(s) após recovery",
                        evento.CodigoJogo, pendentes.Count);
                }
                return;
            }

            var gcLocal = local["GolosCasa"] != null ? Convert.ToInt32(local["GolosCasa"]) : 0;
            var gvLocal = local["GolosVisitante"] != null ? Convert.ToInt32(local["GolosVisitante"]) : 0;

            var estadoMudou = estadoLocal != evento.Estado;
            var marcadorMudou = gcLocal != evento.GolosCasa || gvLocal != evento.GolosFora;

            if (!estadoMudou && !marcadorMudou) return;

            await db.AtualizarJogoAsync(
                evento.CodigoJogo,
                estadoMudou ? (int?)evento.Estado : null,
                marcadorMudou ? (int?)evento.GolosCasa : null,
                marcadorMudou ? (int?)evento.GolosFora : null);

            logger.LogInformation(
                "[Kafka] Jogo atualizado: {CodigoJogo} estado={Estado} marcador={GC}-{GV}",
                evento.CodigoJogo, evento.Estado, evento.GolosCasa, evento.GolosFora);

            // Estados terminais: delegar resolução de apostas ao BetResolutionWorker via RabbitMQ
            if (estadoMudou && evento.Estado is 3 or 4 or 5)
            {
                await rabbit.PublicarResolucaoApostasAsync(evento.CodigoJogo, evento.Estado);
                logger.LogInformation("[Kafka→RabbitMQ] Resolução de apostas enfileirada para {CodigoJogo}", evento.CodigoJogo);
            }
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "[Kafka] Falha ao processar evento para {CodigoJogo}", evento.CodigoJogo);
            throw;
        }
    }

    private sealed record JogoEventoDto(
        string Tipo,
        string CodigoJogo,
        short Estado,
        int GolosCasa,
        int GolosFora,
        string EquipaCasa,
        string EquipaFora,
        string TipoCompeticao,
        DateTime DataHoraInicio,
        DateTime Timestamp);
}
