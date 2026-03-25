namespace ApiResultados.Models;

public sealed class Resultado
{
    public int Id { get; set; }
    public int JogoId { get; set; }
    public int GolosCasa { get; set; }
    public int GolosVisitante { get; set; }
    public string ResultadoJogo { get; set; } = "0";
    public DateTime DataHoraAtualizacao { get; set; }

    public Jogo Jogo { get; set; } = null!;
}
