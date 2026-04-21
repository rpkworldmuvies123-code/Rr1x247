using System.Security.Claims;
using ApiApostas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiApostas.Controllers;

[ApiController]
[Route("api/apostas")]
[Authorize]
public class ApostasController(DatabaseService db) : ControllerBase
{
    public record CriarApostaRequest(string CodigoJogo, string TipoAposta, decimal MontanteApostado, decimal OddMomento);

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;
    private bool IsAdmin => User.IsInRole("Admin");

    [HttpGet]
    public async Task<IActionResult> Listar(
        [FromQuery] string? codigoJogo,
        [FromQuery] int? estado,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim)
    {
        var uid = IsAdmin ? null : UserId;
        var apostas = await db.ConsultarApostasAsync(uid, codigoJogo, estado, dataInicio, dataFim);
        return Ok(apostas);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Obter(int id)
    {
        var uid = IsAdmin ? null : UserId;
        var aposta = await db.ConsultarApostaAsync(id, uid, IsAdmin);
        return Ok(aposta);
    }

    [HttpPost]
    public async Task<IActionResult> Criar([FromBody] CriarApostaRequest req)
    {
        var id = await db.InserirApostaAsync(
            req.CodigoJogo, UserId, req.TipoAposta, req.MontanteApostado, req.OddMomento);
        return Created($"/api/apostas/{id}", new { id });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancelar(int id)
    {
        await db.CancelarApostaAsync(id, UserId, IsAdmin);
        return NoContent();
    }
}
