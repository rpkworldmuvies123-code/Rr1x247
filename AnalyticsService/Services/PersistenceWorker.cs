namespace AnalyticsService.Services;

// Persiste o estado do MetricsStore em disco a cada 30s e no shutdown.
public sealed class PersistenceWorker(MetricsStore store, ILogger<PersistenceWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        logger.LogInformation("PersistenceWorker iniciado — a persistir estado a cada 30s");
        while (!stoppingToken.IsCancellationRequested)
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken).ConfigureAwait(false);
            store.PersistirEstado();
            logger.LogDebug("Estado analytics persistido");
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        store.PersistirEstado();
        logger.LogInformation("Estado analytics persistido no shutdown");
        await base.StopAsync(cancellationToken);
    }
}
