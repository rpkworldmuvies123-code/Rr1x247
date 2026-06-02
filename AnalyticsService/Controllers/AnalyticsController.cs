using AnalyticsService.Services;
using Microsoft.AspNetCore.Mvc;

namespace AnalyticsService.Controllers;

[ApiController]
[Route("api/analytics")]
[Produces("application/json")]
public sealed class AnalyticsController(MetricsStore store) : ControllerBase
{
    /// <summary>Métricas globais em tempo quase real.</summary>
    [HttpGet("resumo")]
    public IActionResult Resumo() => Ok(new
    {
        apostas = new
        {
            ultimoMinuto = store.ApostasPorMinuto(),
            ultimos5Minutos = store.ApostasPorCincoMinutos(),
        },
        utilizadoresAtivos5Min = store.UtilizadoresAtivos5Min(),
        taxasVitoria = store.TaxasVitoria(),
        volumePorCompeticao = store.TodosVolumesPorCompeticao(),
        exposicaoFinanceira = store.TodasExposicoes(),
        volumeTotal = store.VolumeTotal(),
        margemPlataforma = store.MargemPlataforma(),
        timestamp = DateTime.UtcNow,
    });

    /// <summary>Métricas de um jogo específico.</summary>
    [HttpGet("jogos/{codigoJogo}")]
    public IActionResult Jogo(string codigoJogo) => Ok(new
    {
        codigoJogo,
        volumeApostado = store.VolumePorJogo(codigoJogo),
        exposicaoFinanceira = store.TodasExposicoes().TryGetValue(codigoJogo, out var e) ? e : 0,
        timestamp = DateTime.UtcNow,
    });

    /// <summary>Alertas recentes gerados pelo sistema.</summary>
    [HttpGet("alertas")]
    public IActionResult Alertas() => Ok(store.UltimosAlertas());
}
