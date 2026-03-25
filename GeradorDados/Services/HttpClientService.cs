using System.Text;
using System.Text.Json;
using GeradorDados.Models;
using Microsoft.Extensions.Logging;
using Polly;
using Polly.Retry;

namespace GeradorDados.Services;

/// <summary>
/// Serviço de comunicação HTTP com a API de Resultados.
/// Implementa retry policy com backoff exponencial.
/// </summary>
public class HttpClientService
{
    private readonly HttpClient _httpClient;
    private readonly IAsyncPolicy<HttpResponseMessage> _retryPolicy;
    private readonly string _baseUrl;
    private readonly ILogger<HttpClientService> _logger;

    public HttpClientService(string baseUrl, ILogger<HttpClientService> logger)
    {
        _baseUrl = baseUrl;
        _logger = logger;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30) // Timeout de 30 segundos para requisições
        };

        // Configurar retry policy com backoff exponencial
        // 3 tentativas com delay: 1s, 2s, 4s
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => !r.IsSuccessStatusCode)
            .WaitAndRetryAsync(
                retryCount: 3,
                sleepDurationProvider: retryAttempt =>
                    TimeSpan.FromSeconds(Math.Pow(2, retryAttempt - 1)),
                onRetry: (outcome, timespan, retryCount, context) =>
                {
                    _logger.LogWarning(
                        $"Tentativa {retryCount} falhada. Aguardando {timespan.TotalSeconds}s antes de retentar...");
                });
    }

    /// <summary>
    /// Cria um novo jogo na API de Resultados via POST /api/resultados/jogo.
    /// </summary>
    public async Task<int?> CriarJogoAsync(Jogo jogo)
    {
        try
        {
            var json = JsonSerializer.Serialize(jogo);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.PostAsync($"{_baseUrl}/api/resultados/jogo", content)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    $"Erro ao criar jogo {jogo.CodigoJogo}: {response.StatusCode}");
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            _logger.LogInformation(
                $"Jogo {jogo.CodigoJogo} criado com sucesso.");

            // Parse resposta para obter ID (ajustar conforme estrutura da API)
            return ExtrarIdDaResposta(responseBody);
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exceção ao criar jogo {jogo.CodigoJogo}: {ex.Message}");
            return null;
        }
    }

    /// <summary>
    /// Atualiza o resultado/estado de um jogo via PATCH /api/resultados/resultado/{jogoId}.
    /// </summary>
    public async Task<bool> AtualizarResultadoAsync(int jogoId, Resultado resultado)
    {
        try
        {
            var json = JsonSerializer.Serialize(resultado);
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            // Usar método PATCH customizado
            var request = new HttpRequestMessage(HttpMethod.Patch, 
                $"{_baseUrl}/api/resultados/resultado/{jogoId}")
            {
                Content = content
            };

            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.SendAsync(request)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    $"Erro ao atualizar resultado do jogo {jogoId}: {response.StatusCode}");
                return false;
            }

            _logger.LogInformation(
                $"Resultado do jogo {jogoId} atualizado: {resultado.GolosCasa}-{resultado.GolosVisitante}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exceção ao atualizar resultado do jogo {jogoId}: {ex.Message}");
            return false;
        }
    }

    /// <summary>
    /// Extrai o ID da resposta JSON da API.
    /// </summary>
    private int? ExtrarIdDaResposta(string responseBody)
    {
        try
        {
            using var jsonDoc = JsonDocument.Parse(responseBody);
            var root = jsonDoc.RootElement;

            // Procurar propriedade "id" na resposta
            if (root.TryGetProperty("id", out var idElement))
            {
                return idElement.GetInt32();
            }

            return null;
        }
        catch
        {
            return null;
        }
    }

    /// <summary>
    /// Atualiza apenas o estado de um jogo via PATCH /api/resultados/estado/{jogoId}.
    /// </summary>
    public async Task<bool> AtualizarEstadoJogoAsync(int jogoId, int novoEstado)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { estado = novoEstado });
            var content = new StringContent(json, Encoding.UTF8, "application/json");

            var request = new HttpRequestMessage(HttpMethod.Patch, 
                $"{_baseUrl}/api/resultados/estado/{jogoId}")
            {
                Content = content
            };

            var response = await _retryPolicy.ExecuteAsync(async () =>
                await _httpClient.SendAsync(request)
            );

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogError(
                    $"Erro ao atualizar estado do jogo {jogoId}: {response.StatusCode}");
                return false;
            }

            _logger.LogInformation(
                $"Estado do jogo {jogoId} atualizado para: {novoEstado}");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exceção ao atualizar estado do jogo {jogoId}: {ex.Message}");
            return false;
        }
    }
}

