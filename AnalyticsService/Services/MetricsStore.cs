using System.Collections.Concurrent;
using System.Text.Json;

namespace AnalyticsService.Services;

public sealed class MetricsStore
{
    // Janela deslizante de apostas (1h)
    private readonly ConcurrentQueue<BetEvent> _betWindow = new();

    // Exposição financeira por jogo (montante * odd das apostas pendentes)
    private readonly ConcurrentDictionary<string, decimal> _exposicaoPorJogo = new();

    // Volume total apostado
    private readonly ConcurrentDictionary<string, decimal> _volumePorJogo = new();
    private readonly ConcurrentDictionary<string, decimal> _volumePorCompeticao = new();

    // Contagem de resultados resolvidos
    private long _resolvidosGanha1, _resolvidosGanhaX, _resolvidosGanha2, _resolvidosPerdeu;

    // Margem da plataforma: volume apostado − prémios pagos
    private decimal _premiosTotaisPagos;
    private readonly object _premiosLock = new();

    // Janela de vitórias/derrotas recentes para alerta de padrão anómalo (últimos 5 min)
    private readonly ConcurrentQueue<ResultadoRecente> _resultadosRecentes = new();

    // Alertas recentes (circular, max 100)
    private readonly ConcurrentQueue<AlertaEvento> _alertas = new();

    // ── Persistência ─────────────────────────────────────────────────────────
    private static readonly string _persistPath =
        Environment.GetEnvironmentVariable("ANALYTICS_PERSIST_PATH") ?? "/data/analytics-state.json";

    public MetricsStore() => CarregarEstado();

    // ── Escrita ───────────────────────────────────────────────────────────────

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

    public void RegistarApostaResolvida(string tipoAposta, bool ganhou, decimal montante, decimal odd)
    {
        if (ganhou)
        {
            if (tipoAposta == "1") Interlocked.Increment(ref _resolvidosGanha1);
            else if (tipoAposta == "X") Interlocked.Increment(ref _resolvidosGanhaX);
            else if (tipoAposta == "2") Interlocked.Increment(ref _resolvidosGanha2);

            lock (_premiosLock)
                _premiosTotaisPagos += Math.Round(montante * odd, 2);
        }
        else
        {
            Interlocked.Increment(ref _resolvidosPerdeu);
        }

        _resultadosRecentes.Enqueue(new ResultadoRecente(DateTime.UtcNow, ganhou));
        LimparResultadosRecentes();
    }

    public void AdicionarAlerta(string tipo, string mensagem, string? codigoJogo = null)
    {
        _alertas.Enqueue(new AlertaEvento(DateTime.UtcNow, tipo, mensagem, codigoJogo));
        while (_alertas.Count > 100)
            _alertas.TryDequeue(out _);
    }

    // ── Leituras ──────────────────────────────────────────────────────────────

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

    public decimal VolumeTotal() => _volumePorJogo.Values.Sum();

    public decimal MargemPlataforma()
    {
        var volume = VolumeTotal();
        lock (_premiosLock)
            return Math.Round(volume - _premiosTotaisPagos, 2);
    }

    public IReadOnlyDictionary<string, decimal> TodosVolumesPorJogo() => _volumePorJogo;
    public IReadOnlyDictionary<string, decimal> TodosVolumesPorCompeticao() => _volumePorCompeticao;
    public IReadOnlyDictionary<string, decimal> TodasExposicoes() => _exposicaoPorJogo;

    public object TaxasVitoria() => new
    {
        ganhou1 = _resolvidosGanha1,
        ganhouX = _resolvidosGanhaX,
        ganhou2 = _resolvidosGanha2,
        perdeu  = _resolvidosPerdeu,
        total   = _resolvidosGanha1 + _resolvidosGanhaX + _resolvidosGanha2 + _resolvidosPerdeu,
    };

    public (int ganhas, int perdidas) ResultadosUltimos5Min()
    {
        LimparResultadosRecentes();
        var recentes = _resultadosRecentes.ToArray();
        return (recentes.Count(r => r.Ganhou), recentes.Count(r => !r.Ganhou));
    }

    public IReadOnlyList<AlertaEvento> UltimosAlertas() => _alertas.ToArray();

    public int UtilizadoresAtivos5Min()
    {
        LimparJanela();
        var corte = DateTime.UtcNow.AddMinutes(-5);
        return _betWindow.Where(b => b.Timestamp >= corte).Select(b => b.UtilizadorId).Distinct().Count();
    }

    // ── Persistência ──────────────────────────────────────────────────────────

    public void PersistirEstado()
    {
        try
        {
            var dir = Path.GetDirectoryName(_persistPath);
            if (!string.IsNullOrEmpty(dir)) Directory.CreateDirectory(dir);

            var estado = new EstadoPersistido(
                ExposicaoPorJogo:    new Dictionary<string, decimal>(_exposicaoPorJogo),
                VolumePorJogo:       new Dictionary<string, decimal>(_volumePorJogo),
                VolumePorCompeticao: new Dictionary<string, decimal>(_volumePorCompeticao),
                Ganha1:   _resolvidosGanha1,
                GanhaX:   _resolvidosGanhaX,
                Ganha2:   _resolvidosGanha2,
                Perdeu:   _resolvidosPerdeu,
                PremiosTotais: _premiosTotaisPagos
            );
            File.WriteAllText(_persistPath, JsonSerializer.Serialize(estado));
        }
        catch { /* falha silenciosa — não bloquear o serviço */ }
    }

    private void CarregarEstado()
    {
        try
        {
            if (!File.Exists(_persistPath)) return;
            var json = File.ReadAllText(_persistPath);
            var estado = JsonSerializer.Deserialize<EstadoPersistido>(json);
            if (estado is null) return;

            foreach (var (k, v) in estado.ExposicaoPorJogo)    _exposicaoPorJogo[k]    = v;
            foreach (var (k, v) in estado.VolumePorJogo)       _volumePorJogo[k]       = v;
            foreach (var (k, v) in estado.VolumePorCompeticao) _volumePorCompeticao[k] = v;
            _resolvidosGanha1 = estado.Ganha1;
            _resolvidosGanhaX = estado.GanhaX;
            _resolvidosGanha2 = estado.Ganha2;
            _resolvidosPerdeu = estado.Perdeu;
            lock (_premiosLock) _premiosTotaisPagos = estado.PremiosTotais;
        }
        catch { /* estado corrompido — começa do zero */ }
    }

    // ── Limpeza de janelas ────────────────────────────────────────────────────

    private void LimparJanela()
    {
        var corte = DateTime.UtcNow.AddHours(-1);
        while (_betWindow.TryPeek(out var oldest) && oldest.Timestamp < corte)
            _betWindow.TryDequeue(out _);
    }

    private void LimparResultadosRecentes()
    {
        var corte = DateTime.UtcNow.AddMinutes(-5);
        while (_resultadosRecentes.TryPeek(out var oldest) && oldest.Timestamp < corte)
            _resultadosRecentes.TryDequeue(out _);
    }

    // ── Records ───────────────────────────────────────────────────────────────

    public sealed record BetEvent(DateTime Timestamp, decimal Montante, string CodigoJogo, string UtilizadorId);
    public sealed record AlertaEvento(DateTime Timestamp, string Tipo, string Mensagem, string? CodigoJogo);
    private sealed record ResultadoRecente(DateTime Timestamp, bool Ganhou);

    private sealed record EstadoPersistido(
        Dictionary<string, decimal> ExposicaoPorJogo,
        Dictionary<string, decimal> VolumePorJogo,
        Dictionary<string, decimal> VolumePorCompeticao,
        long Ganha1, long GanhaX, long Ganha2, long Perdeu,
        decimal PremiosTotais);
}
