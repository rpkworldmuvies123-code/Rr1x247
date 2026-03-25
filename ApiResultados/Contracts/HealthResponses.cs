namespace ApiResultados.Contracts;

/// <summary>
/// Estatisticas simples de volume de dados da plataforma.
/// </summary>
public sealed class HealthStatsResponse
{
    public long Jogos { get; init; }
    public long Resultados { get; init; }
    public bool DatabaseReachable { get; init; }
    public DateTime TimestampUtc { get; init; }
}
