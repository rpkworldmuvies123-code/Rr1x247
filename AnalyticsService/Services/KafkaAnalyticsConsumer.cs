using System.Text.Json;
using Confluent.Kafka;

namespace AnalyticsService.Services;

public sealed class KafkaAnalyticsConsumer(
    MetricsStore store,
    AlertService alerts,
    IConfiguration config,
    ILogger<KafkaAnalyticsConsumer> logger) : BackgroundService
{
    private const string GroupId = "analytics";

    protected override Task ExecuteAsync(CancellationToken stoppingToken) =>
        Task.Run(() => ConsumeLoop(stoppingToken), stoppingToken);

    private void ConsumeLoop(CancellationToken ct)
    {
        var bootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092";

        var consumerConfig = new ConsumerConfig
        {
            BootstrapServers = bootstrapServers,
            GroupId = GroupId,
            AutoOffsetReset = AutoOffsetReset.Earliest,
            EnableAutoCommit = false,
        };

        using var consumer = new ConsumerBuilder<string, string>(consumerConfig).Build();
        consumer.Subscribe(new[] { "game-events", "bet-events" });

        logger.LogInformation("Analytics consumer iniciado — topics: game-events, bet-events");

        while (!ct.IsCancellationRequested)
        {
            try
            {
                var result = consumer.Consume(ct);
                if (result?.Message?.Value is null) continue;

                if (result.Topic == "game-events")
                    ProcessarGameEvent(result.Message.Value);
                else
                    ProcessarBetEvent(result.Message.Value);

                consumer.Commit(result);
            }
            catch (OperationCanceledException) { break; }
            catch (ConsumeException ex) when (ex.Error.Code == ErrorCode.UnknownTopicOrPart)
            {
                Thread.Sleep(5000);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro ao consumir mensagem Kafka");
                Thread.Sleep(2000);
            }
        }

        consumer.Close();
    }

    private static readonly JsonSerializerOptions _jsonOpts = new() { PropertyNameCaseInsensitive = true };

    private void ProcessarGameEvent(string payload)
    {
        var doc = JsonSerializer.Deserialize<JsonElement>(payload, _jsonOpts);

        var tipo = GetString(doc, "tipo");
        var codigoJogo = GetString(doc, "codigoJogo")!;
        var estado = GetInt(doc, "estado");

        if (tipo == "game-updated" && estado is 3 or 4 or 5)
            store.LimparExposicaoJogo(codigoJogo);
    }

    private void ProcessarBetEvent(string payload)
    {
        var doc = JsonSerializer.Deserialize<JsonElement>(payload, _jsonOpts);

        var tipo = GetString(doc, "tipo");
        var codigoJogo = GetString(doc, "codigoJogo")!;
        var utilizadorId = GetString(doc, "utilizadorId")!;
        var montante = GetDecimal(doc, "montante");
        var odd = GetDecimal(doc, "odd");
        var tipoAposta = GetString(doc, "tipoAposta") ?? "";
        var competicao = GetString(doc, "competicao") ?? "";

        switch (tipo)
        {
            case "bet-placed":
                store.RegistarApostaColocada(codigoJogo, competicao, utilizadorId, montante, odd);
                alerts.VerificarCadenciaApostas();
                var exposicao = store.TodasExposicoes().TryGetValue(codigoJogo, out var e) ? e : 0;
                alerts.VerificarExposicao(codigoJogo, exposicao);
                break;

            case "bet-cancelled":
                store.RegistarApostaCancelada(codigoJogo, montante, odd);
                break;

            case "bet-resolved":
                var estado = GetInt(doc, "estado");
                store.RegistarApostaResolvida(tipoAposta, ganhou: estado == 2);
                break;
        }
    }

    private static string? GetString(JsonElement doc, string key)
    {
        foreach (var prop in doc.EnumerateObject())
            if (prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                return prop.Value.GetString();
        return null;
    }

    private static int GetInt(JsonElement doc, string key)
    {
        foreach (var prop in doc.EnumerateObject())
            if (prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                return prop.Value.GetInt32();
        return 0;
    }

    private static decimal GetDecimal(JsonElement doc, string key)
    {
        foreach (var prop in doc.EnumerateObject())
            if (prop.Name.Equals(key, StringComparison.OrdinalIgnoreCase))
                return prop.Value.GetDecimal();
        return 0;
    }
}
