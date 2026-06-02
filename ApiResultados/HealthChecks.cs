using Confluent.Kafka;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace ApiResultados;

public sealed class SqlServerHealthCheck(IConfiguration config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var host     = Environment.GetEnvironmentVariable("RESULTADOS_DB_HOST") ?? "localhost";
            var port     = int.TryParse(Environment.GetEnvironmentVariable("RESULTADOS_DB_PORT"), out var p) ? p : 1433;
            var database = Environment.GetEnvironmentVariable("RESULTADOS_DB_NAME") ?? "betstrike-resultados";
            var user     = Environment.GetEnvironmentVariable("RESULTADOS_DB_USER") ?? "resultados";
            var password = Environment.GetEnvironmentVariable("RESULTADOS_DB_PASSWORD") ?? "";

            var csb = new SqlConnectionStringBuilder
            {
                DataSource           = $"{host},{port}",
                InitialCatalog       = database,
                UserID               = user,
                Password             = password,
                TrustServerCertificate = true,
                ConnectTimeout       = 5,
            };
            await using var con = new SqlConnection(csb.ConnectionString);
            await con.OpenAsync(ct);
            await using var cmd = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.jogo WHERE estado_atual IN (1,2)", con);
            var ativos = (int)await cmd.ExecuteScalarAsync(ct)!;

            return HealthCheckResult.Healthy("SQL Server OK",
                new Dictionary<string, object> { ["jogos_ativos"] = ativos });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server inacessível", ex);
        }
    }
}

public sealed class KafkaHealthCheck(IConfiguration config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        var bootstrapServers = config["Kafka:BootstrapServers"]
            ?? Environment.GetEnvironmentVariable("Kafka__BootstrapServers")
            ?? "localhost:9092";
        try
        {
            using var admin = new AdminClientBuilder(
                new AdminClientConfig { BootstrapServers = bootstrapServers }).Build();

            var meta = await Task.Run(
                () => admin.GetMetadata(TimeSpan.FromSeconds(5)), ct);

            return HealthCheckResult.Healthy("Kafka OK",
                new Dictionary<string, object>
                {
                    ["broker_count"] = meta.Brokers.Count,
                    ["topics"]       = meta.Topics.Select(t => t.Topic).ToList(),
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Kafka inacessível", ex);
        }
    }
}
