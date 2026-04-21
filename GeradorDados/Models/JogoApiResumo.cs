namespace GeradorDados.Models;

/// <summary>
/// Representa um jogo ja existente na API de Resultados.
/// </summary>
public sealed class JogoApiResumo
{
    public string CodigoJogo { get; set; } = string.Empty;
    public string EquipaCasa { get; set; } = string.Empty;
    public string EquipaFora { get; set; } = string.Empty;
}

