using System.Text;
using System.Text.Json;
using RabbitMQ.Client;

namespace ApiApostas.Services;

public sealed class RabbitMqService : IAsyncDisposable
{
    public const string QueueBetResolution = "bet-resolution";
    public const string QueueBetResolutionDlq = "bet-resolution.dlq";

    private readonly IConnection _connection;
    private readonly IChannel _channel;
    private readonly ILogger<RabbitMqService> _logger;

    private RabbitMqService(IConnection connection, IChannel channel, ILogger<RabbitMqService> logger)
    {
        _connection = connection;
        _channel = channel;
        _logger = logger;
    }

    public static async Task<RabbitMqService> CreateAsync(IConfiguration config, ILogger<RabbitMqService> logger)
    {
        var host = config["RabbitMQ:Host"] ?? "localhost";
        var user = config["RabbitMQ:User"] ?? "guest";
        var password = config["RabbitMQ:Password"] ?? "guest";

        var factory = new ConnectionFactory
        {
            HostName = host,
            UserName = user,
            Password = password,
            AutomaticRecoveryEnabled = true,        // reconecta automaticamente após falha
            NetworkRecoveryInterval = TimeSpan.FromSeconds(5),
            TopologyRecoveryEnabled = true,          // re-declara filas/exchanges após recovery
        };
        var connection = await factory.CreateConnectionAsync();
        var channel = await connection.CreateChannelAsync();

        // DLQ — recebe mensagens que falharam após os retries
        await channel.QueueDeclareAsync(
            queue: QueueBetResolutionDlq,
            durable: true, exclusive: false, autoDelete: false);

        // Fila principal com DLX apontado para a DLQ
        var args = new Dictionary<string, object?>
        {
            ["x-dead-letter-exchange"] = "",
            ["x-dead-letter-routing-key"] = QueueBetResolutionDlq,
            ["x-message-ttl"] = 60_000,   // mensagem expira após 60s se não processada
        };

        await channel.QueueDeclareAsync(
            queue: QueueBetResolution,
            durable: true, exclusive: false, autoDelete: false,
            arguments: args);

        logger.LogInformation("RabbitMQ ligado — host: {Host}", host);
        return new RabbitMqService(connection, channel, logger);
    }

    public async Task PublicarResolucaoApostasAsync(string codigoJogo, int estadoJogo)
    {
        var eventId = Guid.NewGuid();
        var payload = JsonSerializer.Serialize(new { eventId, codigoJogo, estadoJogo });
        var body = Encoding.UTF8.GetBytes(payload);

        var props = new BasicProperties { Persistent = true };

        await _channel.BasicPublishAsync(
            exchange: "",
            routingKey: QueueBetResolution,
            mandatory: false,
            basicProperties: props,
            body: body);

        _logger.LogDebug("Publicado bet-resolution para {CodigoJogo} (estado={Estado})", codigoJogo, estadoJogo);
    }

    public IChannel Channel => _channel;

    public async ValueTask DisposeAsync()
    {
        await _channel.DisposeAsync();
        await _connection.DisposeAsync();
    }
}
