using System.Data;
using Confluent.Kafka;
using Confluent.Kafka.Admin;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using RabbitMQ.Client;

using ApiApostas.Services;

namespace ApiApostas;

public sealed class SqlServerHealthCheck(IConfiguration config) : IHealthCheck
{
    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var cs = config.GetConnectionString("Apostas")!;
            await using var con = new SqlConnection(cs);
            await con.OpenAsync(ct);
            await using var cmd = new SqlCommand("SELECT 1", con);
            await cmd.ExecuteScalarAsync(ct);

            // Contar apostas pendentes como métrica
            await using var cmd2 = new SqlCommand(
                "SELECT COUNT(*) FROM dbo.Apostas WHERE PagamentoPendente = 1", con);
            var pendentes = (int)await cmd2.ExecuteScalarAsync(ct)!;

            return HealthCheckResult.Healthy("SQL Server OK",
                new Dictionary<string, object> { ["apostas_pendentes"] = pendentes });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("SQL Server inacessível", ex);
        }
    }
}

public sealed class RabbitMqHealthCheck(RabbitMqService rabbit) : IHealthCheck
{
    public Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken ct = default)
    {
        try
        {
            var isOpen = rabbit.Channel.IsOpen;
            return Task.FromResult(isOpen
                ? HealthCheckResult.Healthy("RabbitMQ OK",
                    new Dictionary<string, object> { ["channel_open"] = true })
                : HealthCheckResult.Unhealthy("Canal RabbitMQ fechado"));
        }
        catch (Exception ex)
        {
            return Task.FromResult(HealthCheckResult.Unhealthy("RabbitMQ inacessível", ex));
        }
    }
}

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
                new Dictionary<string, object>
                {
                    ["broker_count"] = meta.Brokers.Count,
                    ["topic_count"]  = meta.Topics.Count,
                });
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy("Kafka inacessível", ex);
        }
    }
}
