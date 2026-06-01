using System.Text.Json;
using ApiResultados.Contracts;
using Confluent.Kafka;

namespace ApiResultados.Services;

public sealed class KafkaProducerService : IDisposable
{
    public const string TopicGameEvents = "game-events";

    private readonly IProducer<string, string> _producer;
    private readonly ILogger<KafkaProducerService> _logger;

    public KafkaProducerService(IConfiguration config, ILogger<KafkaProducerService> logger)
    {
        _logger = logger;

        var bootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092";

        var producerConfig = new ProducerConfig
        {
            BootstrapServers = bootstrapServers,
            Acks = Acks.Leader,
            MessageTimeoutMs = 5000,
        };

        _producer = new ProducerBuilder<string, string>(producerConfig).Build();
    }

    public void PublicarJogoEvento(string tipo, Models.Jogo jogo)
    {
        var evento = new JogoEvento
        {
            Tipo = tipo,
            CodigoJogo = jogo.CodigoJogo,
            Estado = jogo.EstadoAtual,
            GolosCasa = jogo.GolosCasaAtual,
            GolosFora = jogo.GolosVisitanteAtual,
            EquipaCasa = jogo.EquipaCasa,
            EquipaFora = jogo.EquipaFora,
            TipoCompeticao = jogo.TipoCompeticao,
            DataHoraInicio = jogo.DataHoraInicio,
            Timestamp = DateTime.UtcNow,
        };

        var payload = JsonSerializer.Serialize(evento);

        // Fire-and-forget: Kafka não bloqueia o fluxo principal
        _producer.Produce(
            TopicGameEvents,
            new Message<string, string> { Key = jogo.CodigoJogo, Value = payload },
            deliveryReport =>
            {
                if (deliveryReport.Error.IsError)
                {
                    _logger.LogWarning(
                        "Falha ao publicar evento Kafka [{Tipo}] para {CodigoJogo}: {Erro}",
                        tipo, jogo.CodigoJogo, deliveryReport.Error.Reason);
                }
            });
    }

    public void Dispose() => _producer.Dispose();
}
