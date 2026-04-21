using System.Security.Claims;
using ApiApostas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiApostas.Controllers;

[ApiController]
[Route("api/pagamentos")]
[Authorize]
public class PagamentosController(DatabaseService db) : ControllerBase
{
    public record DepositarRequest(decimal Valor);

    private string UserId => User.FindFirstValue(ClaimTypes.NameIdentifier)!;

    [HttpGet("saldo")]
    public async Task<IActionResult> Saldo()
    {
        var saldo = await db.ConsultarSaldoAsync(UserId);
        return Ok(saldo);
    }

    [HttpPost("depositar")]
    public async Task<IActionResult> Depositar([FromBody] DepositarRequest req)
    {
        if (req.Valor <= 0)
            return BadRequest(new { message = "O valor do depósito deve ser positivo." });

        await db.DepositarSaldoAsync(UserId, req.Valor);
        var saldo = await db.ConsultarSaldoAsync(UserId);
        return Ok(saldo);
    }
}
