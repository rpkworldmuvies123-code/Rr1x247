using System.ComponentModel.DataAnnotations;

namespace ApiResultados.Contracts;

/// <summary>
/// Pedido para criacao de um novo jogo.
/// </summary>
public sealed class CriarJogoRequest
{
    /// <summary>
    /// Codigo unico do jogo no formato FUT-AAAA-JJNN.
    /// </summary>
    [Required]
    [StringLength(13, MinimumLength = 13)]
    [RegularExpression(@"^FUT-\d{4}-\d{4}$")]
    public string CodigoJogo { get; set; } = string.Empty;

    /// <summary>
    /// Data e hora de inicio previstas para o jogo.
    /// </summary>
    public DateTime DataHoraInicio { get; set; }

    /// <summary>
    /// Nome da equipa da casa.
    /// </summary>
    [Required]
    [StringLength(120)]
    public string EquipaCasa { get; set; } = string.Empty;

    /// <summary>
    /// Nome da equipa visitante.
    /// </summary>
    [Required]
    [StringLength(120)]
    public string EquipaFora { get; set; } = string.Empty;

    /// <summary>
    /// Tipo de competicao (ex.: Liga Portugal).
    /// </summary>
    [Required]
    [StringLength(80)]
    public string TipoCompeticao { get; set; } = string.Empty;

    /// <summary>
    /// Estado inicial do jogo. Deve ser Agendado (1).
    /// </summary>
    [Range(1, 5)]
    public short EstadoAtual { get; set; } = 1;

    /// <summary>
    /// Golos da equipa da casa no payload de criacao (normalmente 0).
    /// </summary>
    [Range(0, int.MaxValue)]
    public int GolosCasa { get; set; } = 0;

    /// <summary>
    /// Golos da equipa visitante no payload de criacao (normalmente 0).
    /// </summary>
    [Range(0, int.MaxValue)]
    public int GolosVisitante { get; set; } = 0;
}

/// <summary>
/// Pedido para atualizacao de estado e/ou marcador de um jogo.
/// </summary>
public sealed class AtualizarJogoRequest
{
    /// <summary>
    /// Novo estado do jogo (1 a 5).
    /// </summary>
    [Range(1, 5)]
    public short? EstadoAtual { get; set; }

    /// <summary>
    /// Novo numero de golos da equipa da casa.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? GolosCasa { get; set; }

    /// <summary>
    /// Novo numero de golos da equipa visitante.
    /// </summary>
    [Range(0, int.MaxValue)]
    public int? GolosVisitante { get; set; }
}
