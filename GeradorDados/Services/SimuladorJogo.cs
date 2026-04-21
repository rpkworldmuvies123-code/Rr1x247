using GeradorDados.Models;

namespace GeradorDados.Services;

/// <summary>
/// Simulador de um jogo de futebol individual.
/// Simula a progressão de um jogo ao longo de 90 minutos, gerando golos aleatoriamente.
/// </summary>
public class SimuladorJogo(
    JogoSimulacao jogo,
    HttpClientService httpClient,
    SimulacaoPortalService portalService,
    ILogger<SimuladorJogo> logger,
    int intervaloAtualizacaoSegundos)
{
    private readonly JogoSimulacao _jogo = jogo;
    private readonly HttpClientService _httpClient = httpClient;
    private readonly ILogger<SimuladorJogo> _logger = logger;
    private readonly SimulacaoPortalService _portalService = portalService;
    private readonly Random _random = new();

    // Intervalo de atualizacao em segundos (10s = 10 minutos no jogo)
    private readonly int _intervaloAtualizacaoSegundos = Math.Max(1, intervaloAtualizacaoSegundos);
    
    // Probabilidades de golos por intervalo para atingir 2-3 golos/jogo
    // Com 9 intervalos: 0.15 * 9 = 1.35 e 0.12 * 9 = 1.08 = ~2.43 golos/jogo
    private const double ProbabilidadeGolosCasa = 0.15;      // ~1.35 golos em 90 minutos
    private const double ProbabilidadeGolosVisitante = 0.12; // ~1.08 golos em 90 minutos

    /// <summary>
    /// Executa a simulação do jogo de forma assíncrona.
    /// </summary>
    public async Task SimularAsync(CancellationToken cancellationToken = default)
    {
        try
        {
            _logger.LogInformation(
                $"[{_jogo.CodigoJogo}] Iniciando simulação: {_jogo.EquipaCasa} vs {_jogo.EquipaFora}");

            // Fase 1: Agendado
            await ExibirEstadoAsync("Agendado (1)");
            await Task.Delay(_intervaloAtualizacaoSegundos * 1000, cancellationToken);

            // Fase 2: Em Curso (90 minutos = 9 intervalos de 10 segundos)
            _jogo.Estado = EstadoJogo.EmCurso;
            await _httpClient.AtualizarEstadoJogoAsync(_jogo.CodigoJogo, (int)EstadoJogo.EmCurso);
            _logger.LogInformation($"[{_jogo.CodigoJogo}] Estado: Em Curso (2)");

            for (int intervalo = 0; intervalo < 9; intervalo++)
            {
                _jogo.MinutoAtual = (intervalo + 1) * 10;

                // Gerar golos aleatoriamente
                GerarGolos();

                // Publicar atualização
                await AtualizarJogoAsync();

                ExibirEstadoAtual();

                // Aguardar intervalo (exceto no último)
                if (intervalo < 8)
                {
                    await Task.Delay(_intervaloAtualizacaoSegundos * 1000, cancellationToken);
                }
            }

            // Fase 3: Finalizado
            _jogo.Estado = EstadoJogo.Finalizado;
            _jogo.MinutoAtual = 90;
            await AtualizarJogoAsync();
            await _httpClient.AtualizarEstadoJogoAsync(_jogo.CodigoJogo, (int)EstadoJogo.Finalizado);
            
            _logger.LogInformation(
                $"[{_jogo.CodigoJogo}] Simulação concluída: {_jogo.EquipaCasa} {_jogo.GolosCasa}-{_jogo.GolosVisitante} {_jogo.EquipaFora}");

            _jogo.Concluido = true;
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning($"[{_jogo.CodigoJogo}] Simulação cancelada.");
        }
        catch (Exception ex)
        {
            _logger.LogError($"[{_jogo.CodigoJogo}] Erro na simulação: {ex.Message}");
        }
    }

    /// <summary>
    /// Gera golos aleatoriamente para ambas as equipas.
    /// </summary>
    private void GerarGolos()
    {
        // Golo casa
        if (_random.NextDouble() < ProbabilidadeGolosCasa)
        {
            _jogo.GolosCasa++;
            _logger.LogInformation(
                $"[{_jogo.CodigoJogo}] ⚽ GOLO! {_jogo.EquipaCasa} {_jogo.GolosCasa}-{_jogo.GolosVisitante}");
            _portalService.RegistarEvento(
                "golo",
                $"{_jogo.EquipaCasa} marcou golo",
                _jogo.CodigoJogo,
                $"marcador={_jogo.GolosCasa}-{_jogo.GolosVisitante}; minuto={_jogo.MinutoAtual}");
        }

        // Golo visitante
        if (_random.NextDouble() < ProbabilidadeGolosVisitante)
        {
            _jogo.GolosVisitante++;
            _logger.LogInformation(
                $"[{_jogo.CodigoJogo}] ⚽ GOLO! {_jogo.EquipaCasa} {_jogo.GolosCasa}-{_jogo.GolosVisitante} {_jogo.EquipaFora}");
            _portalService.RegistarEvento(
                "golo",
                $"{_jogo.EquipaFora} marcou golo",
                _jogo.CodigoJogo,
                $"marcador={_jogo.GolosCasa}-{_jogo.GolosVisitante}; minuto={_jogo.MinutoAtual}");
        }
    }

    /// <summary>
    /// Atualiza o estado do jogo na API de Resultados.
    /// </summary>
    private async Task AtualizarJogoAsync()
    {
        var resultado = new Resultado
        {
            JogoId = _jogo.JogoId,
            GolosCasa = _jogo.GolosCasa,
            GolosVisitante = _jogo.GolosVisitante,
            DataHoraAtualizacao = DateTime.Now
        };

        await _httpClient.AtualizarResultadoAsync(_jogo.CodigoJogo, resultado, _jogo.MinutoAtual);
    }

    /// <summary>
    /// Exibe o estado atual do jogo no console.
    /// </summary>
    private void ExibirEstadoAtual()
    {
        Console.WriteLine(
            $"[{_jogo.CodigoJogo}] {_jogo.MinutoAtual}' | {_jogo.EquipaCasa} {_jogo.GolosCasa}-{_jogo.GolosVisitante} {_jogo.EquipaFora}");
    }

    /// <summary>
    /// Exibe informação de estado no console.
    /// </summary>
    private async Task ExibirEstadoAsync(string estado)
    {
        _logger.LogInformation($"[{_jogo.CodigoJogo}] Estado: {estado}");
        await Task.CompletedTask;
    }
}

