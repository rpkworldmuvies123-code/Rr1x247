using System.Text.Json;
using GeradorDados.Models;
using Microsoft.Extensions.Logging;

namespace GeradorDados.Services;

/// <summary>
/// Serviço para carregar dados de equipas do ficheiro JSON.
/// </summary>
public class EquipasService
{
    private readonly ILogger<EquipasService> _logger;

    public EquipasService(ILogger<EquipasService> logger)
    {
        _logger = logger;
    }

    /// <summary>
    /// Carrega a lista de equipas a partir do ficheiro equipas.json.
    /// </summary>
    public async Task<List<EquipaInfo>> CarregarEquipasAsync(string caminhoJson)
    {
        try
        {
            if (!File.Exists(caminhoJson))
            {
                throw new FileNotFoundException($"Ficheiro de equipas não encontrado: {caminhoJson}");
            }

            var json = await File.ReadAllTextAsync(caminhoJson);
            var equipas = JsonSerializer.Deserialize<List<EquipaInfo>>(json);

            if (equipas == null || equipas.Count == 0)
            {
                throw new InvalidOperationException("Nenhuma equipa foi carregada do JSON.");
            }

            _logger.LogInformation($"Carregadas {equipas.Count} equipas com sucesso.");
            return equipas;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Erro ao carregar equipas: {ex.Message}");
            throw;
        }
    }
}

