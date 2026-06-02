using System.Security.Claims;
using ApiApostas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiApostas.Controllers;

[ApiController]
[Route("api/apostas")]
[Authorize]
public class ApostasController(DatabaseService db, PagamentosClient pagamentos, KafkaProducerService kafka) : ControllerBase
{
    public record CriarApostaRequest(string CodigoJogo, string TipoAposta, decimal MontanteApostado);

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
        // Obter TipoCompeticao do jogo para enriquecer o evento Kafka
        var jogo = await db.ConsultarJogoAsync(req.CodigoJogo);
        var competicao = jogo.TryGetValue("TipoCompeticao", out var tc) ? (tc as string ?? "") : "";

        // 1. Inserir aposta (valida jogo, tipo, montante; devolve a odd aplicada)
        var (apostaId, odd) = await db.InserirApostaAsync(
            req.CodigoJogo, UserId, req.TipoAposta, req.MontanteApostado);

        // 2. Debitar saldo via ApiPagamentos (compensação se falhar)
        try
        {
            await pagamentos.DebitarApostaAsync(UserId, req.MontanteApostado, apostaId);
        }
        catch
        {
            await db.RemoverApostaAsync(apostaId);
            throw new DomainException("Saldo insuficiente ou falha no processamento do pagamento.", 422);
        }

        kafka.Publicar("bet-placed", req.CodigoJogo, UserId, req.TipoAposta, req.MontanteApostado, odd, 1, apostaId, competicao);

        return Created($"/api/apostas/{apostaId}", new { id = apostaId, oddMomento = odd });
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Cancelar(int id)
    {
        // 1. Cancelar aposta na DB (marca PagamentoPendente=1)
        await db.CancelarApostaAsync(id, UserId, IsAdmin);

        // 2. Emitir reembolso via ApiPagamentos
        var aposta = await db.ConsultarApostaAsync(id, null, isAdmin: true);
        var montante = Convert.ToDecimal(aposta["MontanteApostado"]);
        var utilizadorId = (string)aposta["UtilizadorId"]!;
        var competicao = aposta.TryGetValue("TipoCompeticao", out var tc) ? (tc as string ?? "") : "";

        try
        {
            await pagamentos.CreditarApostaAsync(utilizadorId, montante, "RE", id);
            await db.MarcarPagamentoProcessadoAsync(id);
        }
        catch (Exception ex)
        {
            // PagamentoPendente permanece 1 para retry posterior
            _ = ex;
        }

        var codigoJogo = (string)aposta["CodigoJogo"]!;
        var tipoAposta = (string)aposta["TipoAposta"]!;
        var odd = Convert.ToDecimal(aposta["OddMomento"]);
        kafka.Publicar("bet-cancelled", codigoJogo, utilizadorId, tipoAposta, montante, odd, 4, id, competicao);

        return NoContent();
    }
}
