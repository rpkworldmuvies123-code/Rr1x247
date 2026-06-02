namespace AnalyticsService.Services;

public sealed class AlertService(MetricsStore store, IConfiguration config, ILogger<AlertService> logger)
{
    private readonly decimal _limiarExposicao      = config.GetValue<decimal>("Alertas:LimiarExposicaoEuros", 500m);
    private readonly int     _limiarCadencia        = config.GetValue<int>("Alertas:LimiarApostasPorMinuto", 50);
    private readonly decimal _limiarTaxaGanhoAlto   = config.GetValue<decimal>("Alertas:LimiarTaxaGanhoAlto", 0.70m);
    private readonly decimal _limiarTaxaGanhoBaixo  = config.GetValue<decimal>("Alertas:LimiarTaxaGanhoBaixo", 0.10m);
    private readonly int     _minApostasParaAlerta   = config.GetValue<int>("Alertas:MinApostasParaPadrao", 20);

    // 1. Alerta: exposição financeira elevada num jogo
    public void VerificarExposicao(string codigoJogo, decimal exposicaoAtual)
    {
        if (exposicaoAtual < _limiarExposicao) return;
        var mensagem = $"Exposição financeira elevada em {codigoJogo}: {exposicaoAtual:F2} €";
        logger.LogWarning("[ALERTA] {Mensagem}", mensagem);
        store.AdicionarAlerta("exposicao-elevada", mensagem, codigoJogo);
    }

    // 2. Alerta: cadência de apostas elevada (volume/tempo invulgar)
    public void VerificarCadenciaApostas()
    {
        var ppm = store.ApostasPorMinuto();
        if (ppm < _limiarCadencia) return;
        var mensagem = $"Cadência de apostas elevada: {ppm} apostas no último minuto";
        logger.LogWarning("[ALERTA] {Mensagem}", mensagem);
        store.AdicionarAlerta("cadencia-elevada", mensagem);
    }

    // 3. Alerta: padrão anómalo de ganhos/perdas nos últimos 5 minutos
    public void VerificarPadraoResultados()
    {
        var (ganhas, perdidas) = store.ResultadosUltimos5Min();
        var total = ganhas + perdidas;
        if (total < _minApostasParaAlerta) return;

        var taxa = (decimal)ganhas / total;

        if (taxa >= _limiarTaxaGanhoAlto)
        {
            var mensagem = $"Taxa de vitória anormalmente elevada nos últimos 5 min: {taxa:P1} ({ganhas}/{total})";
            logger.LogWarning("[ALERTA] {Mensagem}", mensagem);
            store.AdicionarAlerta("padrao-anomalo-ganhos", mensagem);
        }
        else if (taxa <= _limiarTaxaGanhoBaixo)
        {
            var mensagem = $"Taxa de vitória anormalmente baixa nos últimos 5 min: {taxa:P1} ({ganhas}/{total})";
            logger.LogWarning("[ALERTA] {Mensagem}", mensagem);
            store.AdicionarAlerta("padrao-anomalo-perdas", mensagem);
        }
    }
}
