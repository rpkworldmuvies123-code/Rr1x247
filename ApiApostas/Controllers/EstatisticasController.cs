using ApiApostas.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiApostas.Controllers;

[ApiController]
[Route("api/estatisticas")]
public class EstatisticasController(DatabaseService db) : ControllerBase
{
    [HttpGet("jogos/{codigoJogo}")]
    public async Task<IActionResult> PorJogo(string codigoJogo)
    {
        var stats = await db.EstatisticasJogoAsync(codigoJogo);
        return Ok(stats);
    }

    [HttpGet("competicoes/{tipoCompeticao}")]
    public async Task<IActionResult> PorCompeticao(string tipoCompeticao)
    {
        var stats = await db.EstatisticasCompeticaoAsync(tipoCompeticao);
        return Ok(stats);
    }
}
