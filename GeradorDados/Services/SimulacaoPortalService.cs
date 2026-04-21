using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace GeradorDados.Services;

public sealed class SimulacaoPortalService(
    IServiceScopeFactory scopeFactory,
    ILogger<SimulacaoPortalService> logger,
    int maxEventos)
{
    private readonly object _sync = new();
    private readonly int _maxEventos = Math.Max(1, maxEventos);

    private bool _emExecucao;
    private DateTimeOffset? _ultimaExecucaoInicioUtc;
    private DateTimeOffset? _ultimaExecucaoFimUtc;
    private bool? _ultimaExecucaoSucesso;
    private string _ultimaMensagem = "Sem execuções ainda.";
    private readonly List<EventoSimulacaoDto> _eventos = new();
    private readonly Dictionary<string, JogoEstadoDto> _jogos = new(StringComparer.OrdinalIgnoreCase);

    public EstadoSimulacaoDto ObterEstado()
    {
        lock (_sync)
        {
            return new EstadoSimulacaoDto
            {
                EmExecucao = _emExecucao,
                UltimaExecucaoInicioUtc = _ultimaExecucaoInicioUtc,
                UltimaExecucaoFimUtc = _ultimaExecucaoFimUtc,
                UltimaExecucaoSucesso = _ultimaExecucaoSucesso,
                UltimaMensagem = _ultimaMensagem,
                Eventos = _eventos.ToList(),
                Jogos = _jogos.Values.OrderBy(j => j.CodigoJogo).ToList()
            };
        }
    }

    public void RegistarEvento(string tipo, string mensagem, string? codigoJogo = null, string? detalhe = null)
    {
        lock (_sync)
        {
            _eventos.Add(new EventoSimulacaoDto
            {
                TimestampUtc = DateTimeOffset.UtcNow,
                Tipo = tipo,
                Mensagem = mensagem,
                CodigoJogo = codigoJogo,
                Detalhe = detalhe
            });

            if (_eventos.Count > _maxEventos)
            {
                _eventos.RemoveRange(0, _eventos.Count - _maxEventos);
            }

            _ultimaMensagem = mensagem;
        }

        if (string.Equals(tipo, "erro", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogWarning("[Portal:{Tipo}] {Mensagem} CodigoJogo={CodigoJogo} Detalhe={Detalhe}", tipo, mensagem,
                codigoJogo ?? "-", detalhe ?? "-");
            return;
        }

        if (string.Equals(tipo, "api-request", StringComparison.OrdinalIgnoreCase))
        {
            logger.LogInformation("[Portal:{Tipo}] {Mensagem} CodigoJogo={CodigoJogo}", tipo, mensagem,
                codigoJogo ?? "-");
            return;
        }

        logger.LogInformation("[Portal:{Tipo}] {Mensagem} CodigoJogo={CodigoJogo} Detalhe={Detalhe}", tipo, mensagem,
            codigoJogo ?? "-", detalhe ?? "-");
    }

    public void RegistarJogoGerado(string codigoJogo, string equipaCasa, string equipaFora, DateTime dataHoraInicioUtc)
    {
        lock (_sync)
        {
            _jogos[codigoJogo] = new JogoEstadoDto
            {
                CodigoJogo = codigoJogo,
                EquipaCasa = equipaCasa,
                EquipaFora = equipaFora,
                Estado = "Gerado",
                GolosCasa = 0,
                GolosVisitante = 0,
                MinutoAtual = 0,
                DataHoraInicioUtc = dataHoraInicioUtc.ToUniversalTime(),
                UltimoEnvioApi = "Aguarda criação na API"
            };
        }

        RegistarEvento("jogo-gerado", $"Jogo gerado: {codigoJogo} ({equipaCasa} vs {equipaFora})", codigoJogo,
            $"dataHoraInicio={dataHoraInicioUtc:O}");
    }

    public void AtualizarMarcadorJogo(string codigoJogo, int golosCasa, int golosVisitante, string estado, int? minutoAtual = null)
    {
        lock (_sync)
        {
            if (!_jogos.TryGetValue(codigoJogo, out var jogo))
            {
                jogo = new JogoEstadoDto { CodigoJogo = codigoJogo };
                _jogos[codigoJogo] = jogo;
            }

            jogo.GolosCasa = golosCasa;
            jogo.GolosVisitante = golosVisitante;
            jogo.Estado = estado;
            if (minutoAtual.HasValue)
            {
                jogo.MinutoAtual = Math.Clamp(minutoAtual.Value, 0, 90);
            }
        }
    }

    public void AtualizarEstadoJogo(string codigoJogo, string estado, int? minutoAtual = null)
    {
        lock (_sync)
        {
            if (!_jogos.TryGetValue(codigoJogo, out var jogo))
            {
                jogo = new JogoEstadoDto { CodigoJogo = codigoJogo };
                _jogos[codigoJogo] = jogo;
            }

            jogo.Estado = estado;
            if (minutoAtual.HasValue)
            {
                jogo.MinutoAtual = Math.Clamp(minutoAtual.Value, 0, 90);
            }
        }
    }

    public void RegistarResultadoApi(
        string codigoJogo,
        string operacao,
        int statusCode,
        bool sucesso,
        string payload,
        string resposta)
    {
        var mensagem = sucesso
            ? $"{operacao} OK ({statusCode})"
            : $"{operacao} falhou ({statusCode})";

        lock (_sync)
        {
            if (!_jogos.TryGetValue(codigoJogo, out var jogo))
            {
                jogo = new JogoEstadoDto { CodigoJogo = codigoJogo };
                _jogos[codigoJogo] = jogo;
            }

            jogo.UltimoEnvioApi = mensagem;
        }

        RegistarEvento("api", mensagem, codigoJogo, $"payload={payload}; resposta={resposta}");
    }

    public bool TentarIniciarExecucao(string caminhoEquipas, int intervaloAtualizacaoSegundos)
    {
        lock (_sync)
        {
            if (_emExecucao)
            {
                return false;
            }

            _emExecucao = true;
            _ultimaExecucaoInicioUtc = DateTimeOffset.UtcNow;
            _ultimaExecucaoFimUtc = null;
            _ultimaExecucaoSucesso = null;
            _ultimaMensagem = $"Execução iniciada com intervalo {intervaloAtualizacaoSegundos}s.";
            _jogos.Clear();
        }

        RegistarEvento("separador", "--- Nova simulação iniciada ---");
        RegistarEvento("execucao", "Execução iniciada pelo portal.", detalhe: $"intervalo={intervaloAtualizacaoSegundos}s");

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var orquestrador = scope.ServiceProvider.GetRequiredService<OrquestradorJornada>();
                var sucesso = await orquestrador.ExecutarProximaJornadaAsync(caminhoEquipas, intervaloAtualizacaoSegundos);

                lock (_sync)
                {
                    _ultimaExecucaoSucesso = sucesso;
                    _ultimaMensagem = sucesso
                        ? "Execução concluída com sucesso."
                        : "Execução terminada sem criar/simular jogos (ver logs).";
                }

                RegistarEvento("execucao", sucesso ? "Execução concluída com sucesso." : "Execução terminou sem simulação.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro na execução disparada pelo portal.");
                lock (_sync)
                {
                    _ultimaExecucaoSucesso = false;
                    _ultimaMensagem = $"Erro: {ex.Message}";
                }

                RegistarEvento("erro", $"Erro durante a execução: {ex.Message}");
            }
            finally
            {
                lock (_sync)
                {
                    _emExecucao = false;
                    _ultimaExecucaoFimUtc = DateTimeOffset.UtcNow;
                }
            }
        });

        return true;
    }

    public bool TentarLimparHistorico(out string mensagem)
    {
        lock (_sync)
        {
            if (_emExecucao)
            {
                mensagem = "Não é possível limpar durante uma execução em curso.";
                return false;
            }

            _eventos.Clear();
            _jogos.Clear();
            _ultimaExecucaoInicioUtc = null;
            _ultimaExecucaoFimUtc = null;
            _ultimaExecucaoSucesso = null;
            _ultimaMensagem = "Histórico limpo.";
            mensagem = _ultimaMensagem;
            return true;
        }
    }

    public sealed class EstadoSimulacaoDto
    {
        public bool EmExecucao { get; set; }
        public DateTimeOffset? UltimaExecucaoInicioUtc { get; set; }
        public DateTimeOffset? UltimaExecucaoFimUtc { get; set; }
        public bool? UltimaExecucaoSucesso { get; set; }
        public string UltimaMensagem { get; set; } = string.Empty;
        public IReadOnlyList<EventoSimulacaoDto> Eventos { get; set; } = Array.Empty<EventoSimulacaoDto>();
        public IReadOnlyList<JogoEstadoDto> Jogos { get; set; } = Array.Empty<JogoEstadoDto>();
    }

    public sealed class EventoSimulacaoDto
    {
        public DateTimeOffset TimestampUtc { get; set; }
        public string Tipo { get; set; } = string.Empty;
        public string Mensagem { get; set; } = string.Empty;
        public string? CodigoJogo { get; set; }
        public string? Detalhe { get; set; }
    }

    public sealed class JogoEstadoDto
    {
        public string CodigoJogo { get; set; } = string.Empty;
        public string EquipaCasa { get; set; } = string.Empty;
        public string EquipaFora { get; set; } = string.Empty;
        public DateTimeOffset? DataHoraInicioUtc { get; set; }
        public string Estado { get; set; } = "-";
        public int GolosCasa { get; set; }
        public int GolosVisitante { get; set; }
        public int MinutoAtual { get; set; }
        public string UltimoEnvioApi { get; set; } = string.Empty;
    }
}

