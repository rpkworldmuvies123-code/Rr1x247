namespace ApiApostas.Services;

// Verifica periodicamente se existem apostas com PagamentoPendente=1
// para jogos já em estado terminal. Garante que falhas temporárias de
// RabbitMQ não deixam apostas por resolver — padrão at-least-once delivery.
public sealed class PendingBetsSweeperService(
    IServiceScopeFactory scopeFactory,
    RabbitMqService rabbit,
    ILogger<PendingBetsSweeperService> logger) : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromSeconds(30);

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await Task.Delay(TimeSpan.FromSeconds(15), stoppingToken);

        while (!stoppingToken.IsCancellationRequested)
        {
            try { await VarrerPagamentosPendentes(stoppingToken); }
            catch (Exception ex) when (ex is not OperationCanceledException)
            {
                logger.LogWarning(ex, "[Sweeper] Erro ao varrer pagamentos pendentes");
            }
            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task VarrerPagamentosPendentes(CancellationToken ct)
    {
        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var pendentes = await db.ObterPagamentosPendentesAsync();
        if (pendentes.Count == 0) return;

        // Agrupar por CodigoJogo (campo incluído no SP após fix)
        var porJogo = new Dictionary<string, List<Dictionary<string, object?>>>();
        foreach (var p in pendentes)
        {
            if (!p.TryGetValue("CodigoJogo", out var raw) || raw is not string cod || string.IsNullOrEmpty(cod))
                continue;
            if (!porJogo.ContainsKey(cod)) porJogo[cod] = [];
            porJogo[cod].Add(p);
        }

        if (porJogo.Count == 0) return;

        // Verificar quais jogos estão em estado terminal
        var jogos = await db.ConsultarJogosAsync();
        var jogosMap = jogos
            .Where(j => j.TryGetValue("CodigoJogo", out var k) && k is string)
            .ToDictionary(j => (string)j["CodigoJogo"]!, j => Convert.ToInt32(j["Estado"]));

        foreach (var (codigoJogo, apostas) in porJogo)
        {
            if (!jogosMap.TryGetValue(codigoJogo, out var estadoJogo)) continue;
            if (estadoJogo is not (3 or 4 or 5)) continue; // jogo ainda activo

            try
            {
                await rabbit.PublicarResolucaoApostasAsync(codigoJogo, estadoJogo);
                logger.LogInformation(
                    "[Sweeper] Re-publicado bet-resolution para {CodigoJogo} — {N} aposta(s) pendente(s) (recovery)",
                    codigoJogo, apostas.Count);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "[Sweeper] Falha ao re-publicar para {CodigoJogo}", codigoJogo);
            }
        }
    }
}
