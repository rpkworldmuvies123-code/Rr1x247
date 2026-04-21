using GeradorDados.Models;
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

    private bool _publicandoCalendario;
    private bool _simulandoTodos;
    private readonly HashSet<string> _jogosEmSimulacao = new(StringComparer.OrdinalIgnoreCase);
    private readonly List<JogoSimulacao> _jogosParaSimulacao = new();

    private DateTimeOffset? _ultimaExecucaoInicioUtc;
    private DateTimeOffset? _ultimaExecucaoFimUtc;
    private bool? _ultimaExecucaoSucesso;
    private string _ultimaMensagem = "Sem execuções ainda.";
    private readonly List<EventoSimulacaoDto> _eventos = new();
    private readonly Dictionary<string, JogoEstadoDto> _jogos = new(StringComparer.OrdinalIgnoreCase);

    private bool AlgumEmExecucao => _publicandoCalendario || _simulandoTodos || _jogosEmSimulacao.Count > 0;

    public EstadoSimulacaoDto ObterEstado()
    {
        lock (_sync)
        {
            var emSimulacaoCodes = new HashSet<string>(_jogosEmSimulacao, StringComparer.OrdinalIgnoreCase);
            var simTodos = _simulandoTodos;

            return new EstadoSimulacaoDto
            {
                EmExecucao = AlgumEmExecucao,
                PublicandoCalendario = _publicandoCalendario,
                SimulandoTodos = _simulandoTodos,
                TemJogosParaSimulacao = _jogosParaSimulacao.Count > 0,
                UltimaExecucaoInicioUtc = _ultimaExecucaoInicioUtc,
                UltimaExecucaoFimUtc = _ultimaExecucaoFimUtc,
                UltimaExecucaoSucesso = _ultimaExecucaoSucesso,
                UltimaMensagem = _ultimaMensagem,
                Eventos = _eventos.ToList(),
                Jogos = _jogos.Values
                    .OrderBy(j => j.CodigoJogo)
                    .Select(j => new JogoEstadoDto
                    {
                        CodigoJogo = j.CodigoJogo,
                        EquipaCasa = j.EquipaCasa,
                        EquipaFora = j.EquipaFora,
                        DataHoraInicioUtc = j.DataHoraInicioUtc,
                        Estado = j.Estado,
                        GolosCasa = j.GolosCasa,
                        GolosVisitante = j.GolosVisitante,
                        MinutoAtual = j.MinutoAtual,
                        UltimoEnvioApi = j.UltimoEnvioApi,
                        EmSimulacao = simTodos || emSimulacaoCodes.Contains(j.CodigoJogo)
                    })
                    .ToList()
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
                _eventos.RemoveRange(0, _eventos.Count - _maxEventos);

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
                jogo.MinutoAtual = Math.Clamp(minutoAtual.Value, 0, 90);
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
                jogo.MinutoAtual = Math.Clamp(minutoAtual.Value, 0, 90);
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

    public bool TentarPublicarCalendario(string caminhoEquipas)
    {
        lock (_sync)
        {
            if (AlgumEmExecucao) return false;

            _publicandoCalendario = true;
            _ultimaExecucaoInicioUtc = DateTimeOffset.UtcNow;
            _ultimaExecucaoFimUtc = null;
            _ultimaExecucaoSucesso = null;
            _ultimaMensagem = "A publicar calendário...";
            _jogos.Clear();
            _jogosParaSimulacao.Clear();
        }

        RegistarEvento("separador", "--- Nova publicação de calendário ---");
        RegistarEvento("execucao", "Publicação de calendário iniciada pelo portal.");

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var orquestrador = scope.ServiceProvider.GetRequiredService<OrquestradorJornada>();
                var jogos = await orquestrador.PublicarCalendarioAsync(caminhoEquipas);

                lock (_sync)
                {
                    _jogosParaSimulacao.AddRange(jogos);
                    _ultimaExecucaoSucesso = jogos.Count > 0;
                    _ultimaMensagem = jogos.Count > 0
                        ? $"Calendário publicado: {jogos.Count} jogos criados. Pronto para simulação."
                        : "Nenhum jogo foi criado (ver logs).";
                }

                RegistarEvento("execucao", jogos.Count > 0
                    ? $"Calendário publicado: {jogos.Count} jogos criados."
                    : "Publicação terminou sem criar jogos.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro na publicação do calendário.");
                lock (_sync)
                {
                    _ultimaExecucaoSucesso = false;
                    _ultimaMensagem = $"Erro: {ex.Message}";
                }
                RegistarEvento("erro", $"Erro na publicação: {ex.Message}");
            }
            finally
            {
                lock (_sync)
                {
                    _publicandoCalendario = false;
                    _ultimaExecucaoFimUtc = DateTimeOffset.UtcNow;
                }
            }
        });

        return true;
    }

    public (bool Iniciou, string Mensagem) TentarSimularTodos(int intervaloSegundos)
    {
        List<JogoSimulacao> jogosFrescos;

        lock (_sync)
        {
            if (_publicandoCalendario)
                return (false, "Não é possível simular enquanto o calendário está a ser publicado.");
            if (_simulandoTodos)
                return (false, "Simulação de todos os jogos já está em curso.");
            if (_jogosEmSimulacao.Count > 0)
                return (false, "Existem jogos individuais em simulação. Aguarde a conclusão.");
            if (_jogosParaSimulacao.Count == 0)
                return (false, "Não existem jogos. Gere os emparelhamentos primeiro.");

            jogosFrescos = _jogosParaSimulacao
                .Where(t => _jogos.TryGetValue(t.CodigoJogo, out var j) &&
                            j.Estado.Equals("Agendado", StringComparison.OrdinalIgnoreCase))
                .Select(t => new JogoSimulacao
                {
                    JogoId = t.JogoId,
                    CodigoJogo = t.CodigoJogo,
                    EquipaCasa = t.EquipaCasa,
                    EquipaFora = t.EquipaFora,
                    DataHoraInicio = t.DataHoraInicio,
                    Estado = EstadoJogo.Agendado,
                    GolosCasa = 0,
                    GolosVisitante = 0,
                    MinutoAtual = 0,
                    Concluido = false
                })
                .ToList();

            if (jogosFrescos.Count == 0)
                return (false, "Não existem jogos no estado Agendado para simular.");

            _simulandoTodos = true;
            _ultimaExecucaoInicioUtc = DateTimeOffset.UtcNow;
            _ultimaExecucaoFimUtc = null;
            _ultimaExecucaoSucesso = null;
            _ultimaMensagem = $"A simular {jogosFrescos.Count} jogos em paralelo...";
        }

        RegistarEvento("separador", $"--- Simulação paralela de {jogosFrescos.Count} jogos ---");
        RegistarEvento("execucao", $"Simulação de todos os jogos iniciada.", detalhe: $"intervalo={intervaloSegundos}s");

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var orquestrador = scope.ServiceProvider.GetRequiredService<OrquestradorJornada>();
                await orquestrador.SimularTodosAsync(jogosFrescos, intervaloSegundos);

                lock (_sync)
                {
                    _ultimaExecucaoSucesso = true;
                    _ultimaMensagem = "Simulação concluída com sucesso.";
                }
                RegistarEvento("sucesso", "Simulação de todos os jogos concluída com sucesso.");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro na simulação de todos os jogos.");
                lock (_sync)
                {
                    _ultimaExecucaoSucesso = false;
                    _ultimaMensagem = $"Erro na simulação: {ex.Message}";
                }
                RegistarEvento("erro", $"Erro durante simulação: {ex.Message}");
            }
            finally
            {
                lock (_sync)
                {
                    _simulandoTodos = false;
                    _ultimaExecucaoFimUtc = DateTimeOffset.UtcNow;
                }
            }
        });

        return (true, $"Simulação de {jogosFrescos.Count} jogos iniciada.");
    }

    public (bool Iniciou, string Mensagem) TentarSimularJogo(string codigoJogo, int intervaloSegundos)
    {
        JogoSimulacao jogoFresco;

        lock (_sync)
        {
            if (_publicandoCalendario)
                return (false, "Não é possível simular enquanto o calendário está a ser publicado.");
            if (_simulandoTodos)
                return (false, "Simulação de todos os jogos está em curso.");
            if (_jogosEmSimulacao.Contains(codigoJogo))
                return (false, $"O jogo {codigoJogo} já está a ser simulado.");

            var template = _jogosParaSimulacao.Find(j =>
                j.CodigoJogo.Equals(codigoJogo, StringComparison.OrdinalIgnoreCase));
            if (template == null)
                return (false, $"Jogo {codigoJogo} não encontrado nos emparelhamentos gerados.");

            if (!_jogos.TryGetValue(codigoJogo, out var jogoEstado) ||
                !jogoEstado.Estado.Equals("Agendado", StringComparison.OrdinalIgnoreCase))
                return (false, $"O jogo {codigoJogo} não está no estado Agendado.");

            jogoFresco = new JogoSimulacao
            {
                JogoId = template.JogoId,
                CodigoJogo = template.CodigoJogo,
                EquipaCasa = template.EquipaCasa,
                EquipaFora = template.EquipaFora,
                DataHoraInicio = template.DataHoraInicio,
                Estado = EstadoJogo.Agendado,
                GolosCasa = 0,
                GolosVisitante = 0,
                MinutoAtual = 0,
                Concluido = false
            };

            _jogosEmSimulacao.Add(codigoJogo);
        }

        RegistarEvento("execucao", $"Simulação individual iniciada: {codigoJogo}", codigoJogo,
            $"intervalo={intervaloSegundos}s");

        _ = Task.Run(async () =>
        {
            try
            {
                using var scope = scopeFactory.CreateScope();
                var orquestrador = scope.ServiceProvider.GetRequiredService<OrquestradorJornada>();
                await orquestrador.SimularJogoAsync(jogoFresco, intervaloSegundos);

                RegistarEvento("sucesso", $"Jogo {codigoJogo} simulado com sucesso.", codigoJogo);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Erro na simulação individual do jogo {Codigo}.", codigoJogo);
                RegistarEvento("erro", $"Erro ao simular {codigoJogo}: {ex.Message}", codigoJogo);
            }
            finally
            {
                lock (_sync) _jogosEmSimulacao.Remove(codigoJogo);
            }
        });

        return (true, $"Simulação de {codigoJogo} iniciada.");
    }

    public bool TentarLimparHistorico(out string mensagem)
    {
        lock (_sync)
        {
            if (AlgumEmExecucao)
            {
                mensagem = "Não é possível limpar durante uma execução em curso.";
                return false;
            }

            _eventos.Clear();
            _jogos.Clear();
            _jogosParaSimulacao.Clear();
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
        public bool PublicandoCalendario { get; set; }
        public bool SimulandoTodos { get; set; }
        public bool TemJogosParaSimulacao { get; set; }
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
        public bool EmSimulacao { get; set; }
    }
}
