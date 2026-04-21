using GeradorDados.Models;
using Microsoft.Extensions.Logging;

namespace GeradorDados.Services;

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
    /// Fase 1 do protocolo: gera emparelhamentos e publica os 9 jogos na API como Agendado.
    /// Devolve a lista de JogoSimulacao prontos para simular.
    /// </summary>
    public async Task<List<JogoSimulacao>> PublicarCalendarioAsync(string caminhoEquipas)
    {
        _logger.LogInformation("=====================================");
        _logger.LogInformation("Passo 1: Carregando equipas...");
        _portalService.RegistarEvento("fase", "Passo 1: carregando equipas...");

        var equipas = await _equipasService.CarregarEquipasAsync(caminhoEquipas);
        _logger.LogInformation("✓ {Total} equipas carregadas", equipas.Count);
        _portalService.RegistarEvento("fase", $"Equipas carregadas: {equipas.Count}");

        var jogosExistentes = await _httpClientService.ObterJogosAsync();
        var codigosExistentes = jogosExistentes.Select(j => j.CodigoJogo).ToList();

        var anoAtual = DateTime.UtcNow.Year;
        var numeroJornada = _matchmakingService.ObterProximaJornadaDisponivel(codigosExistentes, anoAtual);

        _logger.LogInformation("PUBLICANDO CALENDÁRIO - JORNADA {Jornada}", numeroJornada);
        _logger.LogInformation("=====================================");
        _portalService.RegistarEvento("separador", $"--- Jornada {numeroJornada} ---");
        _portalService.RegistarEvento("fase", $"Jornada selecionada: {numeroJornada}");

        var emparelhamentos = ConstruirEmparelhamentos(numeroJornada, anoAtual, equipas, jogosExistentes);
        _logger.LogInformation("✓ {Total} jogos preparados para a jornada {Jornada}", emparelhamentos.Count, numeroJornada);
        _portalService.RegistarEvento("fase", $"Emparelhamentos preparados: {emparelhamentos.Count}");

        if (emparelhamentos.Count != NumeroJogos)
            throw new InvalidOperationException(
                $"Foram preparados {emparelhamentos.Count} jogos, mas o protocolo exige {NumeroJogos}.");

        var jogosJaExistentes = _matchmakingService.ContarJogosDaJornada(codigosExistentes, anoAtual, numeroJornada);
        if (jogosJaExistentes > 0)
        {
            _logger.LogWarning(
                "A jornada {Jornada} de {Ano} já tem {Total} jogo(s) na API. Calendário não será publicado novamente.",
                numeroJornada, anoAtual, jogosJaExistentes);
            _portalService.RegistarEvento("alerta",
                $"Jornada {numeroJornada} já tem {jogosJaExistentes} jogo(s) na API. Operação cancelada.");
            return [];
        }

        _logger.LogInformation("Passo 3: Criando jogos na API de Resultados...");
        _portalService.RegistarEvento("fase", "Passo 3: criando jogos na API...");

        var jogos = await CriarJogosAsync(emparelhamentos, numeroJornada);

        _logger.LogInformation("✓ {Total} jogos criados na API", jogos.Count);
        _portalService.RegistarEvento("fase", $"Jogos criados na API: {jogos.Count}");

        if (jogos.Count == 0)
        {
            _logger.LogWarning("Nenhum jogo foi criado na API.");
            _portalService.RegistarEvento("alerta", "Nenhum jogo criado na API.");
        }

        return jogos;
    }

    /// <summary>
    /// Fase 2 do protocolo: simula todos os jogos fornecidos em paralelo.
    /// </summary>
    public async Task SimularTodosAsync(List<JogoSimulacao> jogos, int intervaloSegundos)
    {
        _logger.LogInformation("Iniciando simulação em paralelo de {Total} jogos...", jogos.Count);
        _portalService.RegistarEvento("fase", $"Simulação em paralelo de {jogos.Count} jogos iniciada.");

        var tarefas = jogos.Select(jogo =>
        {
            var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
            var simLogger = loggerFactory.CreateLogger<SimuladorJogo>();
            var simulador = new SimuladorJogo(jogo, _httpClientService, _portalService, simLogger, intervaloSegundos);
            return SimularJogoComTratamentoAsync(simulador, jogo, simLogger);
        }).ToList();

        try
        {
            await Task.WhenAll(tarefas);
        }
        catch (Exception ex)
        {
            _logger.LogError("Erro durante simulação paralela: {Mensagem}", ex.Message);
        }

        ExibirRelatorio(jogos);
        _portalService.RegistarEvento("fase", "Relatório final gerado.");
    }

    /// <summary>
    /// Simula um único jogo.
    /// </summary>
    public async Task SimularJogoAsync(JogoSimulacao jogo, int intervaloSegundos)
    {
        _logger.LogInformation("Iniciando simulação individual: {Codigo}", jogo.CodigoJogo);

        var loggerFactory = LoggerFactory.Create(b => b.AddConsole());
        var simLogger = loggerFactory.CreateLogger<SimuladorJogo>();
        var simulador = new SimuladorJogo(jogo, _httpClientService, _portalService, simLogger, intervaloSegundos);
        await SimularJogoComTratamentoAsync(simulador, jogo, simLogger);
    }

    private List<(string EquipaCasa, string EquipaFora)> ConstruirEmparelhamentos(
        int numeroJornada,
        int anoAtual,
        List<EquipaInfo> equipas,
        IReadOnlyCollection<JogoApiResumo> jogosExistentes)
    {
        if (numeroJornada % 2 == 1)
        {
            _logger.LogInformation("Passo 2: Gerando emparelhamentos aleatórios para jornada ímpar...");
            _portalService.RegistarEvento("fase", "Passo 2: jornada ímpar, emparelhamento aleatório.");
            return _matchmakingService.GerarEmparelhamentos(equipas);
        }

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
            throw new InvalidOperationException(
                $"Não foi possível construir a jornada {numeroJornada}: a jornada {jornadaAnterior} deve ter {NumeroJogos} jogos, mas tem {jogosJornadaAnterior.Count}.");

        return jogosJornadaAnterior
            .Select(x => (x.Jogo.EquipaFora, x.Jogo.EquipaCasa))
            .ToList();
    }

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

            var jogoId = await _httpClientService.CriarJogoAsync(jogo);

            if (jogoId.HasValue && jogoId.Value > 0)
            {
                jogosSimulacao.Add(new JogoSimulacao
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
                });
                _portalService.AtualizarMarcadorJogo(jogo.CodigoJogo, 0, 0, "Agendado");
            }
            else
            {
                _logger.LogWarning("Falha ao criar jogo {Codigo}", jogo.CodigoJogo);
                _portalService.RegistarEvento("erro", $"Falha ao criar jogo {jogo.CodigoJogo}", jogo.CodigoJogo);
            }
        }

        return jogosSimulacao;
    }

    private async Task SimularJogoComTratamentoAsync(
        SimuladorJogo simulador,
        JogoSimulacao jogo,
        ILogger<SimuladorJogo> simLogger)
    {
        try
        {
            await simulador.SimularAsync();
        }
        catch (Exception ex)
        {
            simLogger.LogError("Erro na simulação do jogo {Codigo}: {Mensagem}", jogo.CodigoJogo, ex.Message);
            jogo.Concluido = true;
        }
    }

    private void ExibirRelatorio(List<JogoSimulacao> jogos)
    {
        _logger.LogInformation("");
        _logger.LogInformation("═════════════════════════════════════════");
        _logger.LogInformation("RELATÓRIO FINAL");
        _logger.LogInformation("═════════════════════════════════════════");

        foreach (var jogo in jogos)
        {
            _logger.LogInformation("{Codigo} | {Casa} {GC}-{GF} {Fora}",
                jogo.CodigoJogo, jogo.EquipaCasa, jogo.GolosCasa, jogo.GolosVisitante, jogo.EquipaFora);
        }

        int totalGolos = jogos.Sum(j => j.GolosCasa + j.GolosVisitante);
        double mediaGolos = jogos.Count > 0 ? totalGolos / (double)jogos.Count : 0;

        _logger.LogInformation("");
        _logger.LogInformation("Total de golos: {Total} | Média por jogo: {Media:F2}", totalGolos, mediaGolos);
        _logger.LogInformation("═════════════════════════════════════════");
    }
}
