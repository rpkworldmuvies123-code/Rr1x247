namespace GeradorDados.Models;

/// <summary>
/// Modelo para rastreamento de um jogo durante simulação.
/// </summary>
public class JogoSimulacao
{
    public int JogoId { get; set; }
    public string CodigoJogo { get; set; } = string.Empty;
    public string EquipaCasa { get; set; } = string.Empty;
    public string EquipaFora { get; set; } = string.Empty;
    public int GolosCasa { get; set; }
    public int GolosVisitante { get; set; }
    public EstadoJogo Estado { get; set; } = EstadoJogo.Agendado;
    public int MinutoAtual { get; set; }
    public DateTime DataHoraInicio { get; set; }
    public bool Concluido { get; set; }
}

