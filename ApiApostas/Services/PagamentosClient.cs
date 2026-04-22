using System.Net.Http.Json;

namespace ApiApostas.Services;

public class PagamentosClient(HttpClient httpClient, ILogger<PagamentosClient> logger)
{
    public record SaldoDto(string UtilizadorId, decimal SaldoAtual, string DataHoraAtualizacao);
    public record TransacaoDto(int Id, int? ApostaId, string UtilizadorId, string TipoTransacao, decimal Valor, string DataHora, string Estado);

    private record CriarSaldoPayload(string UtilizadorId);
    private record TransacaoPayload(string UtilizadorId, string Tipo, decimal Valor, int? ApostaId = null);

    // ── Saldo ──────────────────────────────────────────────────────

    public async Task CriarSaldoUtilizadorAsync(string utilizadorId)
    {
        var resp = await httpClient.PostAsJsonAsync("api/pagamentos/utilizadores", new CriarSaldoPayload(utilizadorId));
        await EnsureSuccessAsync(resp, "criar saldo do utilizador");
    }

    public async Task<SaldoDto> ConsultarSaldoAsync(string utilizadorId)
    {
        var resp = await httpClient.GetAsync($"api/pagamentos/utilizadores/{Uri.EscapeDataString(utilizadorId)}/saldo");
        await EnsureSuccessAsync(resp, "consultar saldo");
        return (await resp.Content.ReadFromJsonAsync<SaldoDto>())!;
    }

    public async Task<IEnumerable<SaldoDto>> ConsultarTodosSaldosAsync()
    {
        var resp = await httpClient.GetAsync("api/pagamentos/utilizadores/saldos");
        await EnsureSuccessAsync(resp, "consultar todos os saldos");
        return (await resp.Content.ReadFromJsonAsync<IEnumerable<SaldoDto>>())!;
    }

    // ── Transações ────────────────────────────────────────────────

    /// <summary>Debita o montante da aposta (tipo AP).</summary>
    public async Task<SaldoDto> DebitarApostaAsync(string utilizadorId, decimal valor, int apostaId)
        => await RegistarTransacaoAsync(utilizadorId, "AP", valor, apostaId);

    /// <summary>Credita o prémio (tipo PG) ou reembolso (tipo RE).</summary>
    public async Task<SaldoDto> CreditarApostaAsync(string utilizadorId, decimal valor, string tipo, int apostaId)
        => await RegistarTransacaoAsync(utilizadorId, tipo, valor, apostaId);

    /// <summary>Depósito ou levantamento admin (tipo DE ou LV).</summary>
    public async Task<SaldoDto> AjustarSaldoAsync(string utilizadorId, decimal valor, string tipo)
        => await RegistarTransacaoAsync(utilizadorId, tipo, valor);

    public async Task<IEnumerable<TransacaoDto>> ConsultarTransacoesAsync(
        string utilizadorId, string? tipo = null, int? apostaId = null,
        DateTime? dataInicio = null, DateTime? dataFim = null, int pagina = 1)
    {
        var qs = $"api/pagamentos/utilizadores/{Uri.EscapeDataString(utilizadorId)}/transacoes?pagina={pagina}";
        if (tipo != null) qs += $"&tipo={tipo}";
        if (apostaId.HasValue) qs += $"&apostaId={apostaId}";
        if (dataInicio.HasValue) qs += $"&dataInicio={dataInicio:O}";
        if (dataFim.HasValue) qs += $"&dataFim={dataFim:O}";

        var resp = await httpClient.GetAsync(qs);
        await EnsureSuccessAsync(resp, "consultar transações");
        return (await resp.Content.ReadFromJsonAsync<IEnumerable<TransacaoDto>>())!;
    }

    // ── Private ────────────────────────────────────────────────────

    private async Task<SaldoDto> RegistarTransacaoAsync(string utilizadorId, string tipo, decimal valor, int? apostaId = null)
    {
        var resp = await httpClient.PostAsJsonAsync("api/pagamentos/transacoes",
            new TransacaoPayload(utilizadorId, tipo, valor, apostaId));
        await EnsureSuccessAsync(resp, $"registar transação {tipo}");
        return (await resp.Content.ReadFromJsonAsync<SaldoDto>())!;
    }

    private async Task EnsureSuccessAsync(HttpResponseMessage resp, string operacao)
    {
        if (resp.IsSuccessStatusCode) return;

        string? msg = null;
        try
        {
            var body = await resp.Content.ReadFromJsonAsync<Dictionary<string, string>>();
            body?.TryGetValue("message", out msg);
        }
        catch { /* ignore parse errors */ }

        msg ??= $"Erro ao {operacao} (HTTP {(int)resp.StatusCode}).";
        logger.LogWarning("PagamentosClient: {Operacao} falhou — {Msg}", operacao, msg);

        var statusCode = (int)resp.StatusCode;
        throw new DomainException(msg, statusCode);
    }
}
