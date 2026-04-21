using GeradorDados.Models;
using Microsoft.Extensions.Logging;

namespace GeradorDados.Services;

/// <summary>
/// orquestra a simulação de uma jornada completa.
/// </summary>
public class OrquestradorJornada
{
    private readonly EquipasService _equipasService;
    private readonly MatchmakingService _matchmakingService;
    private readonly HttpClientService _httpClientService;
    private readonly SimulacaoPortalService _portalService;
    private readonly ILogger<OrquestradorJornada> _logger;

    private const int NumeroJogos = 9;

    public OrquestradorJornada(
        EquipasService equipasService,
        MatchmakingService matchmakingService,
        HttpClientService httpClientService,
        SimulacaoPortalService portalService,
        ILogger<OrquestradorJornada> logger)
    {
        _equipasService = equipasService;
        _matchmakingService = matchmakingService;
        _httpClientService = httpClientService;
        _portalService = portalService;
        _logger = logger;
    }

    /// <summary>
    /// Executa a próxima jornada disponível, respeitando as regras do protocolo.
    /// </summary>
    public async Task<bool> ExecutarProximaJornadaAsync(string caminhoEquipas, int intervaloAtualizacaoSegundos)
    {
        try
        {
            _logger.LogInformation("=====================================");
            _logger.LogInformation("Passo 1: Carregando equipas...");
            _portalService.RegistarEvento("fase", "Passo 1: carregando equipas...");
            var equipas = await _equipasService.CarregarEquipasAsync(caminhoEquipas);
            _logger.LogInformation($"✓ {equipas.Count} equipas carregadas");
            _portalService.RegistarEvento("fase", $"Equipas carregadas: {equipas.Count}");

            var jogosExistentes = await _httpClientService.ObterJogosAsync();
            var codigosExistentes = jogosExistentes.Select(j => j.CodigoJogo).ToList();

            var anoAtual = DateTime.UtcNow.Year;
            var numeroJornada = _matchmakingService.ObterProximaJornadaDisponivel(codigosExistentes, anoAtual);

            _logger.LogInformation($"INICIANDO SIMULAÇÃO - JORNADA {numeroJornada}");
            _logger.LogInformation("=====================================");
            _portalService.RegistarEvento("separador", $"--- Jornada {numeroJornada} ---");
            _portalService.RegistarEvento("fase", $"Jornada selecionada: {numeroJornada}");

            // Passo 2: Definir emparelhamentos para a jornada.
            var emparelhamentos = ConstruirEmparelhamentos(numeroJornada, anoAtual, equipas, jogosExistentes);
            _logger.LogInformation($"✓ {emparelhamentos.Count} jogos preparados para a jornada {numeroJornada}");
            _portalService.RegistarEvento("fase", $"Emparelhamentos preparados: {emparelhamentos.Count}");

            if (emparelhamentos.Count != NumeroJogos)
            {
                throw new InvalidOperationException($"Foram preparados {emparelhamentos.Count} jogos, mas o protocolo exige {NumeroJogos}.");
            }

            // Validar se a jornada alvo já existe para evitar criar mais de 9 jogos por jornada.
            var jogosJaExistentesNaJornada = _matchmakingService.ContarJogosDaJornada(codigosExistentes, anoAtual, numeroJornada);
            if (jogosJaExistentesNaJornada > 0)
            {
                _logger.LogWarning(
                    "A jornada {Jornada} de {Ano} já tem {Total} jogo(s) na API. Para respeitar o protocolo, esta execução não vai criar jogos novos.",
                    numeroJornada,
                    anoAtual,
                    jogosJaExistentesNaJornada);
                return false;
            }

            // Passo 3: Criar jogos na API
            _logger.LogInformation("Passo 3: Criando jogos na API de Resultados...");
            _portalService.RegistarEvento("fase", "Passo 3: criando jogos na API...");
            var jogosSimulacao = await CriarJogosAsync(emparelhamentos, numeroJornada);
            _logger.LogInformation($"✓ {jogosSimulacao.Count} jogos criados");
            _portalService.RegistarEvento("fase", $"Jogos criados na API: {jogosSimulacao.Count}");

            if (jogosSimulacao.Count == 0)
            {
                _logger.LogWarning("Nenhum jogo foi criado; a simulação não será iniciada.");
                _portalService.RegistarEvento("alerta", "Nenhum jogo criado; simulação não iniciada.");
                return false;
            }

            // Passo 4: Simular todos os jogos em paralelo
            _logger.LogInformation("Passo 4: Iniciando simulação em paralelo de todos os jogos...");
            _portalService.RegistarEvento("fase", "Passo 4: iniciando simulação em paralelo...");
            await SimularJogosEmParaleloAsync(jogosSimulacao, intervaloAtualizacaoSegundos);

            // Passo 5: Exibir relatório
            ExibirRelatorio(jogosSimulacao, numeroJornada);
            _portalService.RegistarEvento("fase", "Passo 5: relatório final gerado.");

            _logger.LogInformation("=====================================");
            _logger.LogInformation("SIMULAÇÃO CONCLUÍDA COM SUCESSO");
            _logger.LogInformation("=====================================");
            _portalService.RegistarEvento("sucesso", "Simulação concluída com sucesso.");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro durante execução da jornada: {ex.Message}");
            _portalService.RegistarEvento("erro", $"Erro no orquestrador: {ex.Message}");
            throw;
        }
    }

    private List<(string EquipaCasa, string EquipaFora)> ConstruirEmparelhamentos(
        int numeroJornada,
        int anoAtual,
        List<EquipaInfo> equipas,
        IReadOnlyCollection<JogoApiResumo> jogosExistentes)
    {
        // Jornadas ímpares: emparelhamento aleatório no arranque do par (J1, J3, ...).
        if (numeroJornada % 2 == 1)
        {
            _logger.LogInformation("Passo 2: Gerando emparelhamentos aleatórios para jornada ímpar...");
            _portalService.RegistarEvento("fase", "Passo 2: jornada ímpar, emparelhamento aleatório.");
            return _matchmakingService.GerarEmparelhamentos(equipas);
        }

        // Jornadas pares: inverter casa/fora da jornada imediatamente anterior.
        var jornadaAnterior = numeroJornada - 1;
        _logger.LogInformation("Passo 2: Reutilizando emparelhamentos da jornada {JornadaAnterior} com inversão casa/fora...", jornadaAnterior);
        _portalService.RegistarEvento("fase", $"Passo 2: jornada par, inversão casa/fora da jornada {jornadaAnterior}.");

        var jogosJornadaAnterior = jogosExistentes
            .Where(j => _matchmakingService.CodigoEhDaJornada(j.CodigoJogo, anoAtual, jornadaAnterior))
            .Select(j =>
            {
                var ok = MatchmakingService.TryExtrairAnoJornadaNumeroJogo(j.CodigoJogo, out _, out _, out var numeroJogo);
                return new { Jogo = j, NumeroJogo = ok ? numeroJogo : int.MaxValue };
            })
            .OrderBy(x => x.NumeroJogo)
            .ToList();

        if (jogosJornadaAnterior.Count != NumeroJogos)
        {
            throw new InvalidOperationException(
                $"Não foi possível construir a jornada {numeroJornada}: a jornada {jornadaAnterior} deve ter {NumeroJogos} jogos, mas tem {jogosJornadaAnterior.Count}.");
        }

        return jogosJornadaAnterior
            .Select(x => (x.Jogo.EquipaFora, x.Jogo.EquipaCasa))
            .ToList();
    }

    /// <summary>
    /// Cria os jogos na API de Resultados
    /// </summary>
    private async Task<List<JogoSimulacao>> CriarJogosAsync(
        List<(string EquipaCasa, string EquipaFora)> emparelhamentos,
        int numeroJornada)
    {
        var jogosSimulacao = new List<JogoSimulacao>();
        var agora = DateTime.Now;

        for (int i = 0; i < emparelhamentos.Count; i++)
        {
            var (equipaCasa, equipaFora) = emparelhamentos[i];
            int numeroJogo = i + 1;

            var jogo = new Jogo
            {
                CodigoJogo = _matchmakingService.GerarCodigoJogo(numeroJornada, numeroJogo),
                DataHoraInicio = agora,
                EquipaCasa = equipaCasa,
                EquipaFora = equipaFora,
                TipoCompeticao = "1ª Liga",
                EstadoAtual = EstadoJogo.Agendado
            };

            _portalService.RegistarJogoGerado(jogo.CodigoJogo, jogo.EquipaCasa, jogo.EquipaFora, jogo.DataHoraInicio);

            // Criar jogo na API
            var jogoId = await _httpClientService.CriarJogoAsync(jogo);
            
            if (jogoId.HasValue && jogoId.Value > 0)
            {
                var jogoSim = new JogoSimulacao
                {
                    JogoId = jogoId.Value,
                    CodigoJogo = jogo.CodigoJogo,
                    EquipaCasa = equipaCasa,
                    EquipaFora = equipaFora,
                    DataHoraInicio = agora,
                    Estado = EstadoJogo.Agendado,
                    GolosCasa = 0,
                    GolosVisitante = 0,
                    MinutoAtual = 0,
                    Concluido = false
                };

                jogosSimulacao.Add(jogoSim);
                _portalService.AtualizarMarcadorJogo(jogo.CodigoJogo, 0, 0, "Agendado");
            }
            else
            {
                _logger.LogWarning($"Falha ao criar jogo {jogo.CodigoJogo}");
                _portalService.RegistarEvento("erro", $"Falha ao criar jogo {jogo.CodigoJogo}", jogo.CodigoJogo);
            }
        }

        return jogosSimulacao;
    }

    /// <summary>
    /// Simula todos os 9 jogos em paralelo com tratamento de erros agregado
    /// </summary>
    private async Task SimularJogosEmParaleloAsync(List<JogoSimulacao> jogosSimulacao, int intervaloAtualizacaoSegundos)
    {
        var tarefas = jogosSimulacao.Select(jogo =>
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<SimuladorJogo>();
            var simulador = new SimuladorJogo(jogo, _httpClientService, _portalService, logger, intervaloAtualizacaoSegundos);
            
            // Envolver cada tarefa para capturar exceções individualmente
            return SimularJogoComTratamentoAsync(simulador, jogo, logger);
        }).ToList();

        try
        {
            await Task.WhenAll(tarefas);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro durante simulação paralela: {ex.Message}");
            // Task.WhenAll agrega exceções - registar mas continuar para exibir relatório
            _logger.LogWarning("Continuando para exibir relatório final apesar de erros...");
        }
    }

    /// <summary>
    /// Simula um jogo individual com tratamento de erro
    /// </summary>
    private async Task SimularJogoComTratamentoAsync(
        SimuladorJogo simulador, 
        JogoSimulacao jogo,
        ILogger<SimuladorJogo> logger)
    {
        try
        {
            await simulador.SimularAsync();
        }
        catch (Exception ex)
        {
            logger.LogError($"Erro na simulação do jogo {jogo.CodigoJogo}: {ex.Message}");
            jogo.Concluido = true; // Marcar como concluído mesmo com erro para continuar
        }
    }

    /// <summary>
    /// Exibe relatório final da jornada
    /// </summary>
    private void ExibirRelatorio(List<JogoSimulacao> jogos, int numeroJornada)
    {
        _logger.LogInformation("");
        _logger.LogInformation("═════════════════════════════════════════");
        _logger.LogInformation($"RELATÓRIO FINAL - JORNADA {numeroJornada}");
        _logger.LogInformation("═════════════════════════════════════════");

        foreach (var jogo in jogos)
        {
            _logger.LogInformation(
                $"{jogo.CodigoJogo} | {jogo.EquipaCasa} {jogo.GolosCasa}-{jogo.GolosVisitante} {jogo.EquipaFora}");
        }

        // Estatísticas
        int totalGolos = jogos.Sum(j => j.GolosCasa + j.GolosVisitante);
        double mediaGolos = jogos.Count > 0 ? totalGolos / (double)jogos.Count : 0;

        _logger.LogInformation("");
        _logger.LogInformation($"Total de golos na jornada: {totalGolos}");
        _logger.LogInformation($"Média de golos por jogo: {mediaGolos:F2}");
        _logger.LogInformation("═════════════════════════════════════════");
    }
}

