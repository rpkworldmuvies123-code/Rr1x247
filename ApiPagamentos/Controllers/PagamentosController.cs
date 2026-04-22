using ApiPagamentos.Services;
using Microsoft.AspNetCore.Mvc;

namespace ApiPagamentos.Controllers;

/// <summary>
/// Todos os endpoints são protegidos pelo InternalKeyMiddleware (X-Internal-Key).
/// Só a ApiApostas deve chamar esta API directamente.
/// </summary>
[ApiController]
[Route("api/pagamentos")]
public class PagamentosController(DatabaseService db) : ControllerBase
{
    // ── Saldo ──────────────────────────────────────────────────────

    public record CriarSaldoRequest(string UtilizadorId);

    [HttpPost("utilizadores")]
    public async Task<IActionResult> CriarSaldo([FromBody] CriarSaldoRequest req)
    {
        await db.CriarSaldoUtilizadorAsync(req.UtilizadorId);
        var saldo = await db.ConsultarSaldoAsync(req.UtilizadorId);
        return Created($"/api/pagamentos/utilizadores/{req.UtilizadorId}/saldo", saldo);
    }

    [HttpGet("utilizadores/{utilizadorId}/saldo")]
    public async Task<IActionResult> ConsultarSaldo(string utilizadorId)
    {
        var saldo = await db.ConsultarSaldoAsync(utilizadorId);
        return Ok(saldo);
    }

    [HttpGet("utilizadores/saldos")]
    public async Task<IActionResult> ConsultarTodosSaldos()
    {
        var saldos = await db.ConsultarTodosSaldosAsync();
        return Ok(saldos);
    }

    // ── Transações ────────────────────────────────────────────────

    public record RegistarTransacaoRequest(
        string UtilizadorId,
        string Tipo,
        decimal Valor,
        int? ApostaId = null);

    [HttpPost("transacoes")]
    public async Task<IActionResult> RegistarTransacao([FromBody] RegistarTransacaoRequest req)
    {
        if (req.Valor <= 0)
            return BadRequest(new { message = "O valor deve ser positivo." });

        var tiposValidos = new[] { "AP", "PG", "RE", "DE", "LV" };
        if (!tiposValidos.Contains(req.Tipo))
            return BadRequest(new { message = $"Tipo inválido. Use: {string.Join(", ", tiposValidos)}." });

        await db.RegistarTransacaoAsync(req.UtilizadorId, req.Tipo, req.Valor, req.ApostaId);

        var saldo = await db.ConsultarSaldoAsync(req.UtilizadorId);
        return Ok(saldo);
    }

    [HttpGet("utilizadores/{utilizadorId}/transacoes")]
    public async Task<IActionResult> ConsultarTransacoes(
        string utilizadorId,
        [FromQuery] string? tipo,
        [FromQuery] int? apostaId,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] int pagina = 1)
    {
        var transacoes = await db.ConsultarTransacoesAsync(utilizadorId, tipo, apostaId, dataInicio, dataFim, pagina);
        return Ok(transacoes);
    }
}
