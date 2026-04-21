using System.Net.Http.Json;
using ApiApostas.Models;

namespace ApiApostas.Services;

public class SincronizacaoService(
    IServiceScopeFactory scopeFactory,
    IConfiguration configuration,
    ILogger<SincronizacaoService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var baseUrl = configuration["ApiResultados:BaseUrl"] ?? "http://localhost:5204";
        var intervalo = int.TryParse(configuration["Sincronizacao:IntervaloSegundos"], out var s) ? s : 10;

        using var httpClient = new HttpClient { BaseAddress = new Uri(baseUrl) };

        logger.LogInformation("Sincronizacao iniciada — ApiResultados: {BaseUrl}, intervalo: {Intervalo}s", baseUrl, intervalo);

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await SincronizarAsync(httpClient, stoppingToken);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro no ciclo de sincronizacao");
            }

            await Task.Delay(TimeSpan.FromSeconds(intervalo), stoppingToken);
        }
    }

    private async Task SincronizarAsync(HttpClient httpClient, CancellationToken ct)
    {
        var remotos = await httpClient.GetFromJsonAsync<List<JogoResultadosDto>>("api/jogos", ct);
        if (remotos is null || remotos.Count == 0) return;

        await using var scope = scopeFactory.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<DatabaseService>();

        var locais = await db.ConsultarJogosAsync();
        var localPorCodigo = locais.ToDictionary(
            j => (string)j["CodigoJogo"]!,
            j => j);

        foreach (var remoto in remotos)
        {
            try
            {
                if (!localPorCodigo.TryGetValue(remoto.CodigoJogo, out var local))
                {
                    await db.InserirJogoAsync(
                        remoto.CodigoJogo, remoto.DataHoraInicio,
                        remoto.EquipaCasa, remoto.EquipaFora, remoto.TipoCompeticao);
                    logger.LogInformation("Jogo inserido: {CodigoJogo}", remoto.CodigoJogo);
                    continue;
                }

                var estadoLocal = Convert.ToInt32(local["Estado"]);

                // Terminal states cannot be updated
                if (estadoLocal is 3 or 4 or 5) continue;

                var gcLocal = local["GolosCasa"] != null ? Convert.ToInt32(local["GolosCasa"]) : 0;
                var gvLocal = local["GolosVisitante"] != null ? Convert.ToInt32(local["GolosVisitante"]) : 0;

                var estadoMudou = estadoLocal != remoto.EstadoAtual;
                var marcadorMudou = gcLocal != remoto.GolosCasa || gvLocal != remoto.GolosVisitante;

                if (!estadoMudou && !marcadorMudou) continue;

                int? novoEstado = estadoMudou ? remoto.EstadoAtual : null;
                int? novoGc = marcadorMudou ? remoto.GolosCasa : null;
                int? novoGv = marcadorMudou ? remoto.GolosVisitante : null;

                await db.AtualizarJogoAsync(remoto.CodigoJogo, novoEstado, novoGc, novoGv);

                logger.LogInformation(
                    "Jogo atualizado: {CodigoJogo} estado={Estado} marcador={GC}-{GV}",
                    remoto.CodigoJogo, remoto.EstadoAtual, remoto.GolosCasa, remoto.GolosVisitante);
            }
            catch (Exception ex)
            {
                logger.LogWarning(ex, "Falha ao sincronizar jogo {CodigoJogo}", remoto.CodigoJogo);
            }
        }
    }
}
