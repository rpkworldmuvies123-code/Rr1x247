namespace ApiResultados.Contracts;

public sealed class JogoEvento
{
    public string Tipo { get; init; } = string.Empty;
    public string CodigoJogo { get; init; } = string.Empty;
    public short Estado { get; init; }
    public int GolosCasa { get; init; }
    public int GolosFora { get; init; }
    public string EquipaCasa { get; init; } = string.Empty;
    public string EquipaFora { get; init; } = string.Empty;
    public string TipoCompeticao { get; init; } = string.Empty;
    public DateTime DataHoraInicio { get; init; }
    public DateTime Timestamp { get; init; }
}
