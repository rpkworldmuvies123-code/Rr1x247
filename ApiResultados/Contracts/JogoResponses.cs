namespace ApiResultados.Contracts;

/// <summary>
/// Item de jogo para listagens.
/// </summary>
public sealed class JogoListaItemResponse
{
    public int Id { get; init; }
    public string CodigoJogo { get; init; } = string.Empty;
    public DateTime DataHoraInicio { get; init; }
    public string EquipaCasa { get; init; } = string.Empty;
    public string EquipaFora { get; init; } = string.Empty;
    public string TipoCompeticao { get; init; } = string.Empty;
    public short EstadoAtual { get; init; }
    public int GolosCasa { get; init; }
    public int GolosVisitante { get; init; }
    public string Resultado { get; init; } = "0";
    public DateTime? DataHoraAtualizacaoResultado { get; init; }
}

/// <summary>
/// Detalhe completo de um jogo.
/// </summary>
public sealed class JogoDetalheResponse
{
    public int Id { get; init; }
    public string CodigoJogo { get; init; } = string.Empty;
    public DateTime DataHoraInicio { get; init; }
    public string EquipaCasa { get; init; } = string.Empty;
    public string EquipaFora { get; init; } = string.Empty;
    public string TipoCompeticao { get; init; } = string.Empty;
    public short EstadoAtual { get; init; }
    public ResultadoResponse ResultadoAtual { get; init; } = new();
}

/// <summary>
/// Resultado atual do jogo.
/// </summary>
public sealed class ResultadoResponse
{
    public int GolosCasa { get; init; }
    public int GolosVisitante { get; init; }

    /// <summary>
    /// Resultado simbolico: 1 (casa), 2 (fora), X (empate), 0 (desconhecido).
    /// </summary>
    public string Resultado { get; init; } = "0";

    public DateTime DataHoraAtualizacao { get; init; }
}

/// <summary>
/// Resposta da criacao de jogo.
/// </summary>
public sealed class CriarJogoResponse
{
    public int Id { get; init; }
    public string CodigoJogo { get; init; } = string.Empty;
}
