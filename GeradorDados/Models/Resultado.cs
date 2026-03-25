namespace GeradorDados.Models;

/// <summary>
/// Modelo que representa o resultado final de um jogo.
/// </summary>
public class Resultado
{
    public int Id { get; set; }
    public int JogoId { get; set; }
    public int GolosCasa { get; set; }
    public int GolosVisitante { get; set; }
    public DateTime DataHoraAtualizacao { get; set; }
}

