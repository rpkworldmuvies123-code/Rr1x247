using ApiResultados.Contracts;
using ApiResultados.Data;
using ApiResultados.Models;
using ApiResultados.Services;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace ApiResultados.Controllers;

/// <summary>
/// API REST para gestao de jogos e resultados.
/// </summary>
[ApiController]
[Route("api/jogos")]
[Produces("application/json")]
public sealed class JogosController(ResultadosDbContext dbContext, KafkaProducerService kafka) : ControllerBase
{
    /// <summary>
    /// Cria um novo jogo.
    /// </summary>
    /// <remarks>
    /// Rejeita o pedido quando ja existir um jogo com o mesmo <c>CodigoJogo</c>.
    /// O jogo deve ser criado no estado Agendado.
    /// </remarks>
    [HttpPost]
    [ProducesResponseType(typeof(CriarJogoResponse), StatusCodes.Status201Created)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<CriarJogoResponse>> Criar([FromBody] CriarJogoRequest request, CancellationToken cancellationToken)
    {
        var codigoJogo = request.CodigoJogo.Trim();

        if (request.EstadoAtual != (short)EstadoJogo.Agendado)
        {
            return BadRequest(new { message = "Um jogo novo deve ser criado no estado Agendado (1)." });
        }

        var codigoJaExiste = await dbContext.Jogos
            .AnyAsync(j => j.CodigoJogo == codigoJogo, cancellationToken);

        if (codigoJaExiste)
        {
            return Conflict(new { message = "Ja existe um jogo com o codigo indicado." });
        }

        var jogo = new Jogo
        {
            CodigoJogo = codigoJogo,
            DataHoraInicio = request.DataHoraInicio,
            EquipaCasa = request.EquipaCasa.Trim(),
            EquipaFora = request.EquipaFora.Trim(),
            TipoCompeticao = request.TipoCompeticao.Trim(),
            EstadoAtual = request.EstadoAtual,
            GolosCasaAtual = 0,
            GolosVisitanteAtual = 0,
            MarcadorAtualizado = false
        };

        dbContext.Jogos.Add(jogo);
        await dbContext.SaveChangesAsync(cancellationToken);

        kafka.PublicarJogoEvento("game-created", jogo);

        return CreatedAtAction(nameof(ObterPorCodigo), new { codigoJogo = jogo.CodigoJogo }, new CriarJogoResponse
        {
            Id = jogo.Id,
            CodigoJogo = jogo.CodigoJogo
        });
    }

    /// <summary>
    /// Atualiza o estado e/ou o marcador de um jogo existente.
    /// </summary>
    /// <remarks>
    /// Transicoes validas: Agendado -&gt; EmCurso -&gt; Finalizado, com excecoes Agendado -&gt; Cancelado ou Adiado.
    /// O marcador apenas pode ser alterado quando o estado efetivo do pedido e EmCurso.
    /// O registo em <c>resultado</c> so e criado/atualizado ao transitar para Finalizado.
    /// </remarks>
    [HttpPatch("{codigoJogo}")]
    [ProducesResponseType(typeof(JogoDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<ActionResult<JogoDetalheResponse>> Atualizar(
        [FromRoute] string codigoJogo,
        [FromBody] AtualizarJogoRequest request,
        CancellationToken cancellationToken)
    {
        var atualizarMarcador = request.GolosCasa.HasValue || request.GolosVisitante.HasValue;
        if (atualizarMarcador && (!request.GolosCasa.HasValue || !request.GolosVisitante.HasValue))
        {
            return BadRequest(new { message = "Para atualizar o marcador deve indicar golosCasa e golosVisitante." });
        }

        if (!request.EstadoAtual.HasValue && !atualizarMarcador)
        {
            return BadRequest(new { message = "Indique pelo menos o estado ou o marcador para atualizar." });
        }

        var jogo = await dbContext.Jogos
            .Include(j => j.Resultado)
            .FirstOrDefaultAsync(j => j.CodigoJogo == codigoJogo, cancellationToken);

        if (jogo is null)
        {
            return NotFound(new { message = "Jogo nao encontrado." });
        }

        if (request.EstadoAtual.HasValue && !Enum.IsDefined(typeof(EstadoJogo), request.EstadoAtual.Value))
        {
            return BadRequest(new { message = "Estado invalido." });
        }

        var novoEstadoEfetivo = request.EstadoAtual ?? jogo.EstadoAtual;
        if (atualizarMarcador && novoEstadoEfetivo != (short)EstadoJogo.EmCurso)
        {
            return Conflict(new { message = "So e permitido atualizar marcador para jogos em Curso." });
        }

        var estadoAnterior = jogo.EstadoAtual;

        if (request.EstadoAtual.HasValue)
        {
            if (!EstadoTransitionValida(jogo.EstadoAtual, request.EstadoAtual.Value))
            {
                return Conflict(new { message = "Transicao de estado invalida." });
            }

            jogo.EstadoAtual = request.EstadoAtual.Value;
        }

        if (atualizarMarcador)
        {
            jogo.GolosCasaAtual = request.GolosCasa!.Value;
            jogo.GolosVisitanteAtual = request.GolosVisitante!.Value;
            jogo.MarcadorAtualizado = true;
        }

        var transitouParaFinalizado =
            estadoAnterior != (short)EstadoJogo.Finalizado && jogo.EstadoAtual == (short)EstadoJogo.Finalizado;

        if (transitouParaFinalizado)
        {
            if (jogo.Resultado is null)
            {
                jogo.Resultado = new Resultado();
            }

            if (jogo.MarcadorAtualizado)
            {
                jogo.Resultado.GolosCasa = jogo.GolosCasaAtual;
                jogo.Resultado.GolosVisitante = jogo.GolosVisitanteAtual;
                jogo.Resultado.ResultadoJogo = ComputeResultado(jogo.GolosCasaAtual, jogo.GolosVisitanteAtual);
            }
            else
            {
                // Finalizou sem marcador conhecido: guardar resultado desconhecido 0-0.
                jogo.Resultado.GolosCasa = 0;
                jogo.Resultado.GolosVisitante = 0;
                jogo.Resultado.ResultadoJogo = "0";
            }

            jogo.Resultado.DataHoraAtualizacao = DateTime.UtcNow;
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        kafka.PublicarJogoEvento("game-updated", jogo);

        return Ok(MapDetalhe(jogo));
    }

    /// <summary>
    /// Lista jogos com filtros opcionais por intervalo de data e estado.
    /// </summary>
    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<JogoListaItemResponse>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<ActionResult<IReadOnlyList<JogoListaItemResponse>>> Listar(
        [FromQuery] DateTime? dataInicio,
        [FromQuery] DateTime? dataFim,
        [FromQuery] short? estado,
        CancellationToken cancellationToken)
    {
        if (estado.HasValue && !Enum.IsDefined(typeof(EstadoJogo), estado.Value))
        {
            return BadRequest(new { message = "Estado invalido." });
        }

        if (dataInicio.HasValue && dataFim.HasValue && dataInicio > dataFim)
        {
            return BadRequest(new { message = "dataInicio nao pode ser superior a dataFim." });
        }

        var query = dbContext.Jogos
            .AsNoTracking()
            .Include(j => j.Resultado)
            .AsQueryable();

        if (dataInicio.HasValue)
        {
            query = query.Where(j => j.DataHoraInicio >= dataInicio.Value);
        }

        if (dataFim.HasValue)
        {
            query = query.Where(j => j.DataHoraInicio <= dataFim.Value);
        }

        if (estado.HasValue)
        {
            query = query.Where(j => j.EstadoAtual == estado.Value);
        }

        var jogos = await query
            .OrderBy(j => j.DataHoraInicio)
            .Select(j => new JogoListaItemResponse
            {
                Id = j.Id,
                CodigoJogo = j.CodigoJogo,
                DataHoraInicio = j.DataHoraInicio,
                EquipaCasa = j.EquipaCasa,
                EquipaFora = j.EquipaFora,
                TipoCompeticao = j.TipoCompeticao,
                EstadoAtual = j.EstadoAtual,
                GolosCasa = j.EstadoAtual == (short)EstadoJogo.Finalizado && j.Resultado != null
                    ? j.Resultado.GolosCasa
                    : j.GolosCasaAtual,
                GolosVisitante = j.EstadoAtual == (short)EstadoJogo.Finalizado && j.Resultado != null
                    ? j.Resultado.GolosVisitante
                    : j.GolosVisitanteAtual,
                Resultado = j.EstadoAtual == (short)EstadoJogo.Finalizado && j.Resultado != null
                    ? j.Resultado.ResultadoJogo
                    : "0",
                DataHoraAtualizacaoResultado = j.Resultado != null ? j.Resultado.DataHoraAtualizacao : null
            })
            .ToListAsync(cancellationToken);

        return Ok(jogos);
    }

    /// <summary>
    /// Obtem os dados completos de um jogo pelo codigo, incluindo o resultado atual.
    /// </summary>
    [HttpGet("{codigoJogo}")]
    [ProducesResponseType(typeof(JogoDetalheResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<ActionResult<JogoDetalheResponse>> ObterPorCodigo([FromRoute] string codigoJogo, CancellationToken cancellationToken)
    {
        var jogo = await dbContext.Jogos
            .AsNoTracking()
            .Include(j => j.Resultado)
            .FirstOrDefaultAsync(j => j.CodigoJogo == codigoJogo, cancellationToken);

        if (jogo is null)
        {
            return NotFound(new { message = "Jogo nao encontrado." });
        }

        return Ok(MapDetalhe(jogo));
    }

    /// <summary>
    /// Remove um jogo do sistema.
    /// </summary>
    /// <remarks>
    /// Apenas jogos no estado Agendado podem ser removidos.
    /// </remarks>
    [HttpDelete("{codigoJogo}")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    [ProducesResponseType(StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Remover([FromRoute] string codigoJogo, CancellationToken cancellationToken)
    {
        var jogo = await dbContext.Jogos
            .FirstOrDefaultAsync(j => j.CodigoJogo == codigoJogo, cancellationToken);

        if (jogo is null)
        {
            return NotFound(new { message = "Jogo nao encontrado." });
        }

        if (jogo.EstadoAtual != (short)EstadoJogo.Agendado)
        {
            return Conflict(new { message = "So e permitido remover jogos no estado Agendado." });
        }

        dbContext.Jogos.Remove(jogo);
        await dbContext.SaveChangesAsync(cancellationToken);

        return NoContent();
    }

    private static JogoDetalheResponse MapDetalhe(Jogo jogo)
    {
        var jogoFinalizado = jogo.EstadoAtual == (short)EstadoJogo.Finalizado;
        var golosCasa = jogoFinalizado && jogo.Resultado != null ? jogo.Resultado.GolosCasa : jogo.GolosCasaAtual;
        var golosVisitante = jogoFinalizado && jogo.Resultado != null ? jogo.Resultado.GolosVisitante : jogo.GolosVisitanteAtual;
        var resultado = jogoFinalizado && jogo.Resultado != null ? jogo.Resultado.ResultadoJogo : "0";

        return new JogoDetalheResponse
        {
            Id = jogo.Id,
            CodigoJogo = jogo.CodigoJogo,
            DataHoraInicio = jogo.DataHoraInicio,
            EquipaCasa = jogo.EquipaCasa,
            EquipaFora = jogo.EquipaFora,
            TipoCompeticao = jogo.TipoCompeticao,
            EstadoAtual = jogo.EstadoAtual,
            ResultadoAtual = new ResultadoResponse
            {
                GolosCasa = golosCasa,
                GolosVisitante = golosVisitante,
                Resultado = resultado,
                DataHoraAtualizacao = jogo.Resultado?.DataHoraAtualizacao ?? DateTime.MinValue
            }
        };
    }

    private static string ComputeResultado(int golosCasa, int golosVisitante)
    {
        if (golosCasa > golosVisitante)
        {
            return "1";
        }

        if (golosVisitante > golosCasa)
        {
            return "2";
        }

        return "X";
    }

    private static bool EstadoTransitionValida(short estadoAtual, short novoEstado)
    {
        if (estadoAtual == novoEstado)
        {
            return true;
        }

        return ((EstadoJogo)estadoAtual, (EstadoJogo)novoEstado) switch
        {
            // Fluxo principal
            (EstadoJogo.Agendado, EstadoJogo.EmCurso) => true,
            (EstadoJogo.EmCurso, EstadoJogo.Finalizado) => true,

            // Excecoes permitidas apenas para jogos ainda agendados
            (EstadoJogo.Agendado, EstadoJogo.Cancelado) => true,
            (EstadoJogo.Agendado, EstadoJogo.Adiado) => true,

            _ => false
        };
    }
}
