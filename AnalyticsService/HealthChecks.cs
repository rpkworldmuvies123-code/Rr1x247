using Confluent.Kafka;
using Microsoft.Extensions.Diagnostics.HealthChecks;

using AnalyticsService.Services;

namespace AnalyticsService;

public sealed class KafkaHealthCheck(IConfiguration config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var bootstrapServers = config["Kafka:BootstrapServers"] ?? "localhost:9092";
        try
        {
            using var admin = new AdminClientBuilder(
                new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();

            var meta = await Task.Run(
                () => admin.GetMetadata(TimeSpan.FromSeconds(5)), ct);

            return HealthCheckResult.Healthy("Kafka OK",
                new Dictionary<string, object> { ["broker_count"] = meta.Brokers.Count });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Kafka inacessível", ex);
        }
    }
}

public sealed class MetricsStoreHealthCheck(MetricsStore store) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var tv     = store.TaxasVitoria() as dynamic;
        var result = HealthCheckResult.Healthy("MetricsStore OK",
            new Dictionary<string, object>
            {
                ["apostas_por_minuto"]    = store.ApostasPorMinuto(),
                ["utilizadores_ativos"]   = store.UtilizadoresAtivos5Min(),
                ["volume_total"]          = store.VolumeTotal(),
                ["margem_plataforma"]     = store.MargemPlataforma(),
                ["alertas"]               = store.UltimosAlertas().Count,
            });
        return Task.FromResult(result);
    }
}
