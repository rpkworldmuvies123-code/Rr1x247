using System.Collections.Concurrent;

namespace AnalyticsService.Services;

public sealed class MetricsStore
{
    // Janela deslizante de apostas (timestamp, montante, tipoAposta, codigoJogo, utilizadorId)
    private readonly ConcurrentQueue<BetEvent> _betWindow = new();

    // Exposição financeira por jogo (montante * odd das apostas pendentes)
    private readonly ConcurrentDictionary<string, decimal> _exposicaoPorJogo = new();

    // Volume total apostado por jogo
    private readonly ConcurrentDictionary<string, decimal> _volumePorJogo = new();

    // Volume total apostado por competição
    private readonly ConcurrentDictionary<string, decimal> _volumePorCompeticao = new();

    // Contagem de resultados resolvidos (1, X, 2) — para taxa de vitória
    private long _resolvidosGanha1, _resolvidosGanhaX, _resolvidosGanha2, _resolvidosPerdeu;

    // Alertas recentes (circular, max 100)
    private readonly ConcurrentQueue<AlertaEvento> _alertas = new();

    public void RegistarApostaColocada(string codigoJogo, string competicao, string utilizadorId,
        decimal montante, decimal odd)
    {
        _betWindow.Enqueue(new BetEvent(DateTime.UtcNow, montante, codigoJogo, utilizadorId));
        _volumePorJogo.AddOrUpdate(codigoJogo, montante, (_, v) => v + montante);
        _volumePorCompeticao.AddOrUpdate(competicao, montante, (_, v) => v + montante);
        _exposicaoPorJogo.AddOrUpdate(codigoJogo, montante * odd, (_, v) => v + montante * odd);
        LimparJanela();
    }

    public void RegistarApostaCancelada(string codigoJogo, decimal montante, decimal odd)
    {
        _exposicaoPorJogo.AddOrUpdate(codigoJogo, 0, (_, v) => Math.Max(0, v - montante * odd));
    }

    public void RegistarApostaResolvida(string tipoAposta, bool ganhou)
    {
        if (ganhou)
        {
            if (tipoAposta == "1") Interlocked.Increment(ref _resolvidosGanha1);
            else if (tipoAposta == "X") Interlocked.Increment(ref _resolvidosGanhaX);
            else if (tipoAposta == "2") Interlocked.Increment(ref _resolvidosGanha2);
        }
        else
        {
            Interlocked.Increment(ref _resolvidosPerdeu);
        }
    }

    public void LimparExposicaoJogo(string codigoJogo)
    {
        _exposicaoPorJogo.TryRemove(codigoJogo, out _);
    }

    public void AdicionarAlerta(string tipo, string mensagem, string? codigoJogo = null)
    {
        _alertas.Enqueue(new AlertaEvento(DateTime.UtcNow, tipo, mensagem, codigoJogo));
        while (_alertas.Count > 100)
            _alertas.TryDequeue(out _);
    }

    // ── Leituras ──────────────────────────────────────────────────

    public int ApostasPorMinuto()
    {
        LimparJanela();
        var corte = DateTime.UtcNow.AddMinutes(-1);
        return _betWindow.Count(b => b.Timestamp >= corte);
    }

    public int ApostasPorCincoMinutos()
    {
        LimparJanela();
        var corte = DateTime.UtcNow.AddMinutes(-5);
        return _betWindow.Count(b => b.Timestamp >= corte);
    }

    public decimal VolumePorJogo(string codigoJogo) =>
        _volumePorJogo.TryGetValue(codigoJogo, out var v) ? v : 0;

    public IReadOnlyDictionary<string, decimal> TodosVolumesPorJogo() => _volumePorJogo;
    public IReadOnlyDictionary<string, decimal> TodosVolumesPorCompeticao() => _volumePorCompeticao;
    public IReadOnlyDictionary<string, decimal> TodasExposicoes() => _exposicaoPorJogo;

    public object TaxasVitoria() => new
    {
        ganhou1 = _resolvidosGanha1,
        ganhouX = _resolvidosGanhaX,
        ganhou2 = _resolvidosGanha2,
        perdeu = _resolvidosPerdeu,
        total = _resolvidosGanha1 + _resolvidosGanhaX + _resolvidosGanha2 + _resolvidosPerdeu,
    };

    public IReadOnlyList<AlertaEvento> UltimosAlertas() => _alertas.ToArray();

    public int UtilizadoresAtivos5Min()
    {
        LimparJanela();
        var corte = DateTime.UtcNow.AddMinutes(-5);
        return _betWindow.Where(b => b.Timestamp >= corte).Select(b => b.UtilizadorId).Distinct().Count();
    }

    private void LimparJanela()
    {
        var corte = DateTime.UtcNow.AddHours(-1);
        while (_betWindow.TryPeek(out var oldest) && oldest.Timestamp < corte)
            _betWindow.TryDequeue(out _);
    }

    public sealed record BetEvent(DateTime Timestamp, decimal Montante, string CodigoJogo, string UtilizadorId);
    public sealed record AlertaEvento(DateTime Timestamp, string Tipo, string Mensagem, string? CodigoJogo);
}
