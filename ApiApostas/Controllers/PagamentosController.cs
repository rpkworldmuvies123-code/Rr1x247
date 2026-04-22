using System.Security.Claims;
using ApiApostas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiApostas.Controllers;

/// <summary>
/// Proxy entre o frontend e a ApiPagamentos.
/// O frontend usa o JWT emitido por esta API; esta API converte em X-Internal-Key.
/// </summary>
[ApiController]
[Route("api/pagamentos")]
[Authorize]
public class PagamentosController(PagamentosClient pagamentos) : ControllerBase
{
    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("saldo")]
    public async Task<IActionResult> Saldo()
    {
        var saldo = await pagamentos.ConsultarSaldoAsync(UserId);
        return Ok(saldo);
    }

    [HttpGet("transacoes")]
    public async Task<IActionResult> Transacoes(
        [FromQuery] string? tipo,
        [FromQuery] int? apostaId,
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] int pagina = 1)
    {
        var transacoes = await pagamentos.ConsultarTransacoesAsync(UserId, tipo, apostaId, dataInicio, dataFim, pagina);
        return Ok(transacoes);
    }
}
