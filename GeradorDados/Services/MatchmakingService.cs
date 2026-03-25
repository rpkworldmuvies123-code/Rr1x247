using GeradorDados.Models;

namespace GeradorDados.Services;

/// <summary>
/// Serviço responsável por gerar emparelhamentos aleatórios de jogos.
/// </summary>
public class MatchmakingService
{
    private readonly Random _random = new();

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
        var emparelhamentos = new List<(string, string)>();

        // Algoritmo: dividir em 2 grupos de 9, depois emparelhar
        // Garantir rotação para alternância casa/fora
        var grupo1 = nomes.Skip(0).Take(9).ToList();
        var grupo2 = nomes.Skip(9).Take(9).ToList();

        // Embaralhar grupo2 para adicionar aleatoriedade
        Embaralhar(grupo2);

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
}

