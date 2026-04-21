namespace ApiApostas.Models;

public sealed class JogoResultadosDto
{
    public string CodigoJogo { get; init; } = string.Empty;
    public DateTime DataHoraInicio { get; init; }
    public string EquipaCasa { get; init; } = string.Empty;
    public string EquipaFora { get; init; } = string.Empty;
    public string TipoCompeticao { get; init; } = string.Empty;
    public short EstadoAtual { get; init; }
    public int GolosCasa { get; init; }
    public int GolosVisitante { get; init; }
}
