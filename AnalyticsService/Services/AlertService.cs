namespace AnalyticsService.Services;

public sealed class AlertService(MetricsStore store, IConfiguration config, ILogger<AlertService> logger)
{
    private readonly decimal _limiarExposicao = config.GetValue<decimal>("Alertas:LimiarExposicaoEuros", 500m);
    private readonly int _limiarApostasPorMinuto = config.GetValue<int>("Alertas:LimiarApostasPorMinuto", 50);

    public void VerificarExposicao(string codigoJogo, decimal exposicaoAtual)
    {
        if (exposicaoAtual < _limiarExposicao) return;

        var mensagem = $"Exposição financeira elevada em {codigoJogo}: {exposicaoAtual:C}";
        logger.LogWarning("[ALERTA] {Mensagem}", mensagem);
        store.AdicionarAlerta("exposicao-elevada", mensagem, codigoJogo);
    }

    public void VerificarCadenciaApostas()
    {
        var ppm = store.ApostasPorMinuto();
        if (ppm < _limiarApostasPorMinuto) return;

        var mensagem = $"Cadência de apostas elevada: {ppm} apostas no último minuto";
        logger.LogWarning("[ALERTA] {Mensagem}", mensagem);
        store.AdicionarAlerta("cadencia-elevada", mensagem);
    }
}
