using ApiApostas.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace ApiApostas.Controllers;

[ApiController]
[Route("api/jogos")]
public class JogosController(DatabaseService db, PagamentosClient pagamentos, ILogger<JogosController> logger) : ControllerBase
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

    [HttpGet("{codigoJogo}/odds")]
    public async Task<IActionResult> ObterOdds(string codigoJogo)
    {
        var odds = await db.ConsultarOddsJogoAsync(codigoJogo);
        return Ok(odds);
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

        // Processar pagamentos pendentes resultantes da transição de estado
        await ProcessarPagamentosPendentesAsync(codigoJogo);

        return NoContent();
    }

    [HttpDelete("{codigoJogo}")]
    [Authorize(Roles = "Admin")]
    public async Task<IActionResult> Remover(string codigoJogo)
    {
        await db.RemoverJogoAsync(codigoJogo);
        return NoContent();
    }

    public record ApostaResolvida(int ApostaId, string UtilizadorId, string TipoAposta, decimal Montante, decimal Odd, int Estado);

    public async Task<List<ApostaResolvida>> ProcessarPagamentosPendentesAsync(string codigoJogo)
    {
        var resolvidas = new List<ApostaResolvida>();

        List<System.Collections.Generic.Dictionary<string, object?>> pendentes;
        try
        {
            pendentes = await db.ObterPagamentosPendentesAsync(codigoJogo);
        }
        catch (Exception ex)
        {
            logger.LogWarning(ex, "Falha ao obter pagamentos pendentes para {CodigoJogo}", codigoJogo);
            return resolvidas;
        }

        foreach (var bet in pendentes)
        {
            var apostaId = Convert.ToInt32(bet["Id"]);
            var utilizadorId = (string)bet["UtilizadorId"]!;
            var estado = Convert.ToInt32(bet["Estado"]);
            var montante = Convert.ToDecimal(bet["MontanteApostado"]);
            var odd = Convert.ToDecimal(bet["OddMomento"]);
            var tipoAposta = (string)(bet.TryGetValue("TipoAposta", out var t) ? t : null) ?? "";

            try
            {
                if (estado == 2)
                {
                    var premio = Math.Round(montante * odd, 2);
                    await pagamentos.CreditarApostaAsync(utilizadorId, premio, "PG", apostaId);
                }
                else if (estado == 4)
                {
                    await pagamentos.CreditarApostaAsync(utilizadorId, montante, "RE", apostaId);
                }

                await db.MarcarPagamentoProcessadoAsync(apostaId);
                resolvidas.Add(new ApostaResolvida(apostaId, utilizadorId, tipoAposta, montante, odd, estado));

                logger.LogInformation(
                    "Pagamento processado: aposta {ApostaId} estado={Estado} utilizador={Uid}",
                    apostaId, estado, utilizadorId);
            }
            catch (Exception ex)
            {
                logger.LogError(ex,
                    "Falha ao processar pagamento da aposta {ApostaId} — será reprocessado",
                    apostaId);
            }
        }

        return resolvidas;
    }
}
