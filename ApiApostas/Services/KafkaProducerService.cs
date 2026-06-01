using System.Text.Json;
using Confluent.Kafka;

namespace ApiApostas.Services;

public sealed class KafkaProducerService : IDisposable
{
    public const string TopicBetEvents = "bet-events";

    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(IConfiguration config, ILogger<KafkaProducerService> logger)
    {
        _logger = logger;
        var bootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092";
        _producer = new ProducerBuilder<string, string>(new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.Leader,
            MessageTimeoutMs = 5000,
        }).Build();
    }

    public void Publicar(string tipo, string codigoJogo, string utilizadorId,
        string tipoAposta, decimal montante, decimal odd, int estado, int apostaId)
    {
        var payload = JsonSerializer.Serialize(new
        {
            tipo,
            apostaId,
            codigoJogo,
            utilizadorId,
            tipoAposta,
            montante,
            odd,
            estado,
            timestamp = DateTime.UtcNow,
        });

        _producer.Produce(
            TopicBetEvents,
            new Message<string, string> { Key = codigoJogo, Value = payload },
            report =>
            {
                if (report.Error.IsError)
                    _logger.LogWarning("[Kafka] Falha ao publicar bet-event [{Tipo}] aposta {Id}: {Erro}",
                        tipo, apostaId, report.Error.Reason);
            });
    }

    public void Dispose() => _producer.Dispose();
}
