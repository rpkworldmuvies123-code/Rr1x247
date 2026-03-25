namespace GeradorDados.Models;

/// <summary>
/// Modelo que representa um jogo de futebol.
/// </summary>
public class Jogo
{
    public int Id { get; set; }
    public string CodigoJogo { get; set; } = string.Empty; // FUT-AAAA-JJNN
    public DateTime DataHoraInicio { get; set; }
    public string EquipaCasa { get; set; } = string.Empty;
    public string EquipaFora { get; set; } = string.Empty;
    public string TipoCompeticao { get; set; } = "1ª Liga";
    public EstadoJogo EstadoAtual { get; set; } = EstadoJogo.Agendado;
}

