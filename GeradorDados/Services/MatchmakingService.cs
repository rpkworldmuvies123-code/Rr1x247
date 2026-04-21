using GeradorDados.Models;
using System.Text.RegularExpressions;

namespace GeradorDados.Services;

/// <summary>
/// Serviço responsável por gerar emparelhamentos aleatórios de jogos.
/// </summary>
public class MatchmakingService
{
    private readonly Random _random = new();
    private static readonly Regex CodigoJogoRegex = new(@"^FUT-(\d{4})-(\d{2})(\d{2})$", RegexOptions.Compiled);

    /// <summary>
    /// Gera 9 emparelhamentos aleatórios para uma jornada.
    /// Garante que:
    /// - Nenhuma equipa é emparelhada consigo própria
    /// - Cada equipa aparece exatamente uma vez como casa e uma vez como fora
    /// </summary>
    public List<(string EquipaCasa, string EquipaFora)> GerarEmparelhamentos(List<EquipaInfo> equipas)
    {
        if (equipas.Count != 18)
            throw new ArgumentException("Deve haver exatamente 18 equipas para gerar uma jornada com 9 jogos.");

        var nomes = equipas.Select(e => e.Nome).ToList();
        Embaralhar(nomes);
        var emparelhamentos = new List<(string, string)>();

        // Algoritmo: dividir em 2 grupos de 9 e emparelhar aleatoriamente.
        var grupo1 = nomes.Skip(0).Take(9).ToList();
        var grupo2 = nomes.Skip(9).Take(9).ToList();

        // Gerar emparelhamentos: grupo1 em casa, grupo2 fora
        for (int i = 0; i < 9; i++)
        {
            emparelhamentos.Add((grupo1[i], grupo2[i]));
        }

        return emparelhamentos;
    }

    /// <summary>
    /// Embaralha uma lista in-place usando Fisher-Yates shuffle.
    /// </summary>
    private void Embaralhar<T>(List<T> lista)
    {
        for (int i = lista.Count - 1; i > 0; i--)
        {
            int indiceAleatorio = _random.Next(0, i + 1);
            (lista[i], lista[indiceAleatorio]) = (lista[indiceAleatorio], lista[i]);
        }
    }

    /// <summary>
    /// Gera o código único para um jogo no formato FUT-AAAA-JJNN.
    /// AAAA = ano corrente
    /// JJ = número da jornada (com 2 dígitos)
    /// NN = número do jogo dentro da jornada
    /// </summary>
    public string GerarCodigoJogo(int jornada, int numeroJogo)
    {
        int ano = DateTime.Now.Year;
        return $"FUT-{ano:D4}-{jornada:D2}{numeroJogo:D2}";
    }

    /// <summary>
    /// Determina a proxima jornada disponivel para o ano indicado.
    /// </summary>
    public int ObterProximaJornadaDisponivel(IEnumerable<string> codigosExistentes, int ano)
    {
        var maxJornada = 0;

        foreach (var codigo in codigosExistentes)
        {
            if (!TryExtrairAnoJornada(codigo, out var anoCodigo, out var jornadaCodigo))
            {
                continue;
            }

            if (anoCodigo == ano && jornadaCodigo > maxJornada)
            {
                maxJornada = jornadaCodigo;
            }
        }

        return maxJornada + 1;
    }

    /// <summary>
    /// Conta quantos jogos ja existem para uma jornada especifica de um ano.
    /// </summary>
    public int ContarJogosDaJornada(IEnumerable<string> codigosExistentes, int ano, int jornada)
    {
        var total = 0;

        foreach (var codigo in codigosExistentes)
        {
            if (!TryExtrairAnoJornada(codigo, out var anoCodigo, out var jornadaCodigo))
            {
                continue;
            }

            if (anoCodigo == ano && jornadaCodigo == jornada)
            {
                total++;
            }
        }

        return total;
    }

    /// <summary>
    /// Indica se o codigo pertence ao ano e jornada indicados.
    /// </summary>
    public bool CodigoEhDaJornada(string codigoJogo, int ano, int jornada)
    {
        return TryExtrairAnoJornadaNumeroJogo(codigoJogo, out var anoCodigo, out var jornadaCodigo, out _)
               && anoCodigo == ano
               && jornadaCodigo == jornada;
    }

    /// <summary>
    /// Extrai ano, jornada e numero do jogo a partir do codigo FUT-AAAA-JJNN.
    /// </summary>
    public static bool TryExtrairAnoJornadaNumeroJogo(string codigoJogo, out int ano, out int jornada, out int numeroJogo)
    {
        ano = 0;
        jornada = 0;
        numeroJogo = 0;

        if (string.IsNullOrWhiteSpace(codigoJogo))
        {
            return false;
        }

        var match = CodigoJogoRegex.Match(codigoJogo);
        if (!match.Success)
        {
            return false;
        }

        ano = int.Parse(match.Groups[1].Value);
        jornada = int.Parse(match.Groups[2].Value);
        numeroJogo = int.Parse(match.Groups[3].Value);
        return true;
    }

    private static bool TryExtrairAnoJornada(string codigoJogo, out int ano, out int jornada)
    {
        return TryExtrairAnoJornadaNumeroJogo(codigoJogo, out ano, out jornada, out _);
    }
}

