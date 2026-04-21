using ApiApostas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiApostas.Controllers;

[ApiController]
[Route("api/resultados")]
public class ResultadosController(DatabaseService db) : ControllerBase
{
    public record InserirResultadoRequest(string CodigoJogo, int GolosCasa, int GolosVisitante);

    [HttpGet("{codigoJogo}")]
    public async Task<IActionResult> Obter(string codigoJogo)
    {
        var resultado = await db.ConsultarResultadoJogoAsync(codigoJogo);
        return Ok(resultado);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Inserir([FromBody] InserirResultadoRequest req)
    {
        var id = await db.InserirResultadoAsync(req.CodigoJogo, req.GolosCasa, req.GolosVisitante);
        return Created($"/api/resultados/{req.CodigoJogo}", new { id, codigoJogo = req.CodigoJogo });
    }
}
