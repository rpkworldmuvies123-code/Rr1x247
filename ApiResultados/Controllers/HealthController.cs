using ApiResultados.Contracts;
using ApiResultados.Data;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiResultados.Controllers;

/// <summary>
/// Endpoints de monitorizacao da API.
/// </summary>
[ApiController]
[Route("health")]
[Produces("application/json")]
public sealed class HealthController(ResultadosDbContext dbContext) : ControllerBase
{
    /// <summary>
    /// Verifica liveness da API.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(string), StatusCodes.Status200OK)]
    public ActionResult<string> Get()
    {
        return Ok("ok");
    }

    /// <summary>
    /// Devolve contagens de registos nas tabelas de jogo e resultado.
    /// </summary>
    [HttpGet("stats")]
    [ProducesResponseType(typeof(HealthStatsResponse), StatusCodes.Status200OK)]
    public async Task<ActionResult<HealthStatsResponse>> GetStats(CancellationToken cancellationToken)
    {
        var jogos = await dbContext.Jogos.LongCountAsync(cancellationToken);
        var resultados = await dbContext.Resultados.LongCountAsync(cancellationToken);

        return Ok(new HealthStatsResponse
        {
            Jogos = jogos,
            Resultados = resultados,
            DatabaseReachable = true,
            TimestampUtc = DateTime.UtcNow
        });
    }
}
