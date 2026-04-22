using System.Security.Claims;
using ApiApostas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiApostas.Controllers;

[ApiController]
[Route("api/admin")]
[Authorize(Roles = "Admin")]
public class AdminController(DatabaseService db, PagamentosClient pagamentos) : ControllerBase
{
    public record AjustarSaldoRequest(decimal Valor, string Motivo);

    private string AdminId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("utilizadores")]
    public async Task<IActionResult> ListarUtilizadores()
    {
        var utilizadores = await db.ConsultarUtilizadoresAsync();

        // Enriquecer com saldos via ApiPagamentos
        var saldos = await pagamentos.ConsultarTodosSaldosAsync();
        var saldoMap = saldos.ToDictionary(s => s.UtilizadorId, s => s);

        var resultado = utilizadores.Select(u =>
        {
            var id = u["Id"]?.ToString() ?? "";
            var saldo = saldoMap.TryGetValue(id, out var s) ? s.SaldoAtual : (decimal?)null;
            return new Dictionary<string, object?>(u)
            {
                ["SaldoAtual"] = saldo,
                ["SaldoAtualizadoEm"] = saldoMap.TryGetValue(id, out var sv) ? sv.DataHoraAtualizacao : null
            };
        });

        return Ok(resultado);
    }

    [HttpPost("utilizadores/{utilizadorId}/saldo")]
    public async Task<IActionResult> AjustarSaldo(string utilizadorId, [FromBody] AjustarSaldoRequest req)
    {
        if (req.Valor == 0)
            return BadRequest(new { message = "O valor do ajuste não pode ser zero." });
        if (string.IsNullOrWhiteSpace(req.Motivo))
            return BadRequest(new { message = "O motivo é obrigatório." });

        var tipo = req.Valor > 0 ? "DE" : "LV";
        var saldo = await pagamentos.AjustarSaldoAsync(utilizadorId, Math.Abs(req.Valor), tipo);

        var acao = req.Valor > 0 ? "AJUSTE_CREDITO" : "AJUSTE_DEBITO";
        var detalhe = $"{req.Motivo} | {Math.Abs(req.Valor):F2} EUR";
        await db.LogarAcaoAdminAsync(AdminId, utilizadorId, acao, detalhe);

        return Ok(saldo);
    }

    [HttpGet("log")]
    public async Task<IActionResult> Log(
        [FromQuery] string? adminId,
        [FromQuery] string? utilizadorId,
        [FromQuery] int? pagina)
    {
        var log = await db.ConsultarLogAdminAsync(adminId, utilizadorId, pagina);
        return Ok(log);
    }
}
