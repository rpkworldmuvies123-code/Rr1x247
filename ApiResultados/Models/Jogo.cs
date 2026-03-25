namespace ApiResultados.Models;

public sealed class Jogo
{
    public int Id { get; set; }
    public string CodigoJogo { get; set; } = string.Empty;
    public DateTime DataHoraInicio { get; set; }
    public string EquipaCasa { get; set; } = string.Empty;
    public string EquipaFora { get; set; } = string.Empty;
    public string TipoCompeticao { get; set; } = string.Empty;
    public short EstadoAtual { get; set; }
    public int GolosCasaAtual { get; set; }
    public int GolosVisitanteAtual { get; set; }
    public bool MarcadorAtualizado { get; set; }

    public Resultado? Resultado { get; set; }
}
