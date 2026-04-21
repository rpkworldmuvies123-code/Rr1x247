using ApiApostas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiApostas.Controllers;

[ApiController]
[Route("api/jogos")]
public class JogosController(DatabaseService db) : ControllerBase
{
    public record CriarJogoRequest(
        string CodigoJogo,
        DateTime DataHoraInicio,
        string EquipaCasa,
        string EquipaFora,
        string TipoCompeticao);

    public record AtualizarJogoRequest(int? Estado, int? GolosCasa, int? GolosVisitante);

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] DateTime? data,
        [FromQuery] int? estado,
        [FromQuery] string? tipoCompeticao)
    {
        var jogos = await db.ConsultarJogosAsync(data, estado, tipoCompeticao);
        return Ok(jogos);
    }

    [HttpGet("{codigoJogo}")]
    public async Task<IActionResult> Obter(string codigoJogo)
    {
        var jogo = await db.ConsultarJogoAsync(codigoJogo);
        return Ok(jogo);
    }

    [HttpPost]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Criar([FromBody] CriarJogoRequest req)
    {
        var id = await db.InserirJogoAsync(
            req.CodigoJogo, req.DataHoraInicio,
            req.EquipaCasa, req.EquipaFora, req.TipoCompeticao);
        return Created($"/api/jogos/{req.CodigoJogo}", new { id, codigoJogo = req.CodigoJogo });
    }

    [HttpPatch("{codigoJogo}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Atualizar(string codigoJogo, [FromBody] AtualizarJogoRequest req)
    {
        await db.AtualizarJogoAsync(codigoJogo, req.Estado, req.GolosCasa, req.GolosVisitante);
        return NoContent();
    }

    [HttpDelete("{codigoJogo}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Remover(string codigoJogo)
    {
        await db.RemoverJogoAsync(codigoJogo);
        return NoContent();
    }
}
