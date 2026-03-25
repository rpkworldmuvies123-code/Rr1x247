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
    private readonly ILogger<OrquestradorJornada> _logger;

    private const int NumeroJornada = 1;
    private const int NumeroJogos = 9;

    public OrquestradorJornada(
        EquipasService equipasService,
        MatchmakingService matchmakingService,
        HttpClientService httpClientService,
        ILogger<OrquestradorJornada> logger)
    {
        _equipasService = equipasService;
        _matchmakingService = matchmakingService;
        _httpClientService = httpClientService;
        _logger = logger;
    }

    /// <summary>
    /// Executa a simulação completa da Jornada 1
    /// </summary>
    public async Task ExecutarJornadaAsync(string caminhoEquipas)
    {
        try
        {
            _logger.LogInformation("=====================================");
            _logger.LogInformation("INICIANDO SIMULAÇÃO - JORNADA 1");
            _logger.LogInformation("=====================================");

            // Passo 1: Carregar equipas
            _logger.LogInformation("Passo 1: Carregando equipas...");
            var equipas = await _equipasService.CarregarEquipasAsync(caminhoEquipas);
            _logger.LogInformation($"✓ {equipas.Count} equipas carregadas");

            // Passo 2: Gerar emparelhamentos
            _logger.LogInformation("Passo 2: Gerando emparelhamentos aleatórios...");
            var emparelhamentos = _matchmakingService.GerarEmparelhamentos(equipas);
            _logger.LogInformation($"✓ {emparelhamentos.Count} jogos gerados para a jornada");

            // Passo 3: Criar jogos na API
            _logger.LogInformation("Passo 3: Criando jogos na API de Resultados...");
            var jogosSimulacao = await CriarJogosAsync(emparelhamentos);
            _logger.LogInformation($"✓ {jogosSimulacao.Count} jogos criados");

            // Passo 4: Simular todos os jogos em paralelo
            _logger.LogInformation("Passo 4: Iniciando simulação em paralelo de todos os jogos...");
            await SimularJogosEmParaleloAsync(jogosSimulacao);

            // Passo 5: Exibir relatório
            ExibirRelatório(jogosSimulacao);

            _logger.LogInformation("=====================================");
            _logger.LogInformation("SIMULAÇÃO CONCLUÍDA COM SUCESSO");
            _logger.LogInformation("=====================================");
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro durante execução da jornada: {ex.Message}");
            throw;
        }
    }

    /// <summary>
    /// Cria os jogos na API de Resultados
    /// </summary>
    private async Task<List<JogoSimulacao>> CriarJogosAsync(
        List<(string EquipaCasa, string EquipaFora)> emparelhamentos)
    {
        var jogosSimulacao = new List<JogoSimulacao>();
        var agora = DateTime.Now;

        for (int i = 0; i < emparelhamentos.Count; i++)
        {
            var (equipaCasa, equipaFora) = emparelhamentos[i];
            int numeroJogo = i + 1;

            var jogo = new Jogo
            {
                CodigoJogo = _matchmakingService.GerarCodigoJogo(NumeroJornada, numeroJogo),
                DataHoraInicio = agora,
                EquipaCasa = equipaCasa,
                EquipaFora = equipaFora,
                TipoCompeticao = "1ª Liga",
                EstadoAtual = EstadoJogo.Agendado
            };

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
            }
            else
            {
                _logger.LogWarning($"Falha ao criar jogo {jogo.CodigoJogo}");
            }
        }

        return jogosSimulacao;
    }

    /// <summary>
    /// Simula todos os 9 jogos em paralelo com tratamento de erros agregado
    /// </summary>
    private async Task SimularJogosEmParaleloAsync(List<JogoSimulacao> jogosSimulacao)
    {
        var tarefas = jogosSimulacao.Select(jogo =>
        {
            var loggerFactory = LoggerFactory.Create(builder => builder.AddConsole());
            var logger = loggerFactory.CreateLogger<SimuladorJogo>();
            var simulador = new SimuladorJogo(jogo, _httpClientService, logger);
            
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
    private void ExibirRelatório(List<JogoSimulacao> jogos)
    {
        _logger.LogInformation("");
        _logger.LogInformation("═════════════════════════════════════════");
        _logger.LogInformation("RELATÓRIO FINAL - JORNADA 1");
        _logger.LogInformation("═════════════════════════════════════════");

        foreach (var jogo in jogos)
        {
            _logger.LogInformation(
                $"{jogo.CodigoJogo} | {jogo.EquipaCasa} {jogo.GolosCasa}-{jogo.GolosVisitante} {jogo.EquipaFora}");
        }

        // Estatísticas
        int totalGolos = jogos.Sum(j => j.GolosCasa + j.GolosVisitante);
        double mediaGolos = totalGolos / (double)jogos.Count;

        _logger.LogInformation("");
        _logger.LogInformation($"Total de golos na jornada: {totalGolos}");
        _logger.LogInformation($"Média de golos por jogo: {mediaGolos:F2}");
        _logger.LogInformation("═════════════════════════════════════════");
    }
}

