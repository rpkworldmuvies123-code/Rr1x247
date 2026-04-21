using System.Text;
using System.Text.Json;
using GeradorDados.Models;
using Microsoft.Extensions.Logging;
using System.Net;
using Polly;

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
    private readonly SimulacaoPortalService _portalService;
    private readonly string? _apiKey;
    private readonly string _clientId;
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public HttpClientService(
        string baseUrl,
        ILogger<HttpClientService> logger,
        string? apiKey,
        string? clientId,
        SimulacaoPortalService portalService)
    {
        _baseUrl = baseUrl.TrimEnd('/');
        _logger = logger;
        _portalService = portalService;
        _apiKey = string.IsNullOrWhiteSpace(apiKey) ? null : apiKey;
        _clientId = string.IsNullOrWhiteSpace(clientId) ? "gerador-dados" : clientId;
        _httpClient = new HttpClient
        {
            Timeout = TimeSpan.FromSeconds(30) // Timeout de 30 segundos para requisições
        };

        // Configurar retry policy com backoff exponencial
        // 3 tentativas com delay: 1s, 2s, 4s
        _retryPolicy = Policy
            .Handle<HttpRequestException>()
            .OrResult<HttpResponseMessage>(r => IsTransientStatusCode(r.StatusCode))
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
    /// Obtem os jogos atualmente registados na API.
    /// </summary>
    public async Task<IReadOnlyCollection<JogoApiResumo>> ObterJogosAsync()
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"{_baseUrl}/api/jogos");
            var response = await _httpClient.SendAsync(request);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning(
                    "Nao foi possivel ler jogos existentes. HTTP {StatusCode}. A assumir lista vazia.",
                    (int)response.StatusCode);
                return Array.Empty<JogoApiResumo>();
            }

            var body = await response.Content.ReadAsStringAsync();
            var jogos = JsonSerializer.Deserialize<List<JogoApiResumo>>(body, JsonOptions) ?? new List<JogoApiResumo>();
            return jogos;
        }
        catch (Exception ex)
        {
            _logger.LogWarning("Falha ao obter jogos existentes: {Message}. A assumir lista vazia.", ex.Message);
            return Array.Empty<JogoApiResumo>();
        }
    }

    /// <summary>
    /// Obtem apenas os codigos de jogos atualmente registados na API.
    /// </summary>
    public async Task<IReadOnlyCollection<string>> ObterCodigosJogosAsync()
    {
        var jogos = await ObterJogosAsync();
        return jogos
            .Select(j => j.CodigoJogo)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Cria um novo jogo na API de Resultados via POST /api/jogos.
    /// </summary>
    public async Task<int?> CriarJogoAsync(Jogo jogo)
    {
        try
        {
            var json = JsonSerializer.Serialize(jogo);
            _portalService.RegistarEvento("api-request", "POST /api/jogos", jogo.CodigoJogo, json);

            var response = await SendWithRetryAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Post, $"{_baseUrl}/api/jogos")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                AddAuthHeaders(request);
                return request;
            });

            if (!response.IsSuccessStatusCode)
            {
                var errorBody = await response.Content.ReadAsStringAsync();
                _portalService.RegistarResultadoApi(jogo.CodigoJogo, "POST /api/jogos", (int)response.StatusCode, false, json,
                    Truncar(errorBody));
                _logger.LogError(
                    "Erro ao criar jogo {CodigoJogo}: HTTP {StatusCode} - {Response}",
                    jogo.CodigoJogo,
                    (int)response.StatusCode,
                    errorBody);
                return null;
            }

            var responseBody = await response.Content.ReadAsStringAsync();
            _portalService.RegistarResultadoApi(jogo.CodigoJogo, "POST /api/jogos", (int)response.StatusCode, true, json,
                Truncar(responseBody));
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
    /// Atualiza o marcador de um jogo via PATCH /api/jogos/{codigoJogo}.
    /// </summary>
    public async Task<bool> AtualizarResultadoAsync(string codigoJogo, Resultado resultado, int minutoAtual)
    {
        try
        {
            var payload = new
            {
                golosCasa = resultado.GolosCasa,
                golosVisitante = resultado.GolosVisitante
            };
            var json = JsonSerializer.Serialize(payload);
            _portalService.RegistarEvento("api-request", "PATCH /api/jogos (marcador)", codigoJogo, json);

            var response = await SendWithRetryAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Patch, $"{_baseUrl}/api/jogos/{codigoJogo}")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                AddAuthHeaders(request);
                return request;
            });

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _portalService.RegistarResultadoApi(codigoJogo, "PATCH /api/jogos (marcador)", (int)response.StatusCode, false,
                    json, Truncar(responseBody));
                _logger.LogError(
                    "Erro ao atualizar marcador do jogo {CodigoJogo}: HTTP {StatusCode} - {Response}",
                    codigoJogo,
                    (int)response.StatusCode,
                    responseBody);
                return false;
            }

            _logger.LogInformation(
                $"Marcador do jogo {codigoJogo} atualizado: {resultado.GolosCasa}-{resultado.GolosVisitante}");
            _portalService.AtualizarMarcadorJogo(codigoJogo, resultado.GolosCasa, resultado.GolosVisitante, "Em Curso", minutoAtual);
            _portalService.RegistarResultadoApi(codigoJogo, "PATCH /api/jogos (marcador)", (int)response.StatusCode, true, json,
                "OK");
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exceção ao atualizar marcador do jogo {codigoJogo}: {ex.Message}");
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
    /// Atualiza apenas o estado de um jogo via PATCH /api/jogos/{codigoJogo}.
    /// </summary>
    public async Task<bool> AtualizarEstadoJogoAsync(string codigoJogo, int novoEstado)
    {
        try
        {
            var json = JsonSerializer.Serialize(new { estadoAtual = novoEstado });
            _portalService.RegistarEvento("api-request", "PATCH /api/jogos (estado)", codigoJogo, json);

            var response = await SendWithRetryAsync(() =>
            {
                var request = new HttpRequestMessage(HttpMethod.Patch, $"{_baseUrl}/api/jogos/{codigoJogo}")
                {
                    Content = new StringContent(json, Encoding.UTF8, "application/json")
                };

                AddAuthHeaders(request);
                return request;
            });

            if (!response.IsSuccessStatusCode)
            {
                var responseBody = await response.Content.ReadAsStringAsync();
                _portalService.RegistarResultadoApi(codigoJogo, "PATCH /api/jogos (estado)", (int)response.StatusCode, false,
                    json, Truncar(responseBody));
                _logger.LogError(
                    "Erro ao atualizar estado do jogo {CodigoJogo}: HTTP {StatusCode} - {Response}",
                    codigoJogo,
                    (int)response.StatusCode,
                    responseBody);
                return false;
            }

            _logger.LogInformation(
                $"Estado do jogo {codigoJogo} atualizado para: {novoEstado}");
            _portalService.RegistarResultadoApi(codigoJogo, "PATCH /api/jogos (estado)", (int)response.StatusCode, true,
                json, "OK");
            var minutoAtual = novoEstado == 3 ? 90 : 0;
            _portalService.AtualizarEstadoJogo(codigoJogo, NomeEstado(novoEstado), minutoAtual);
            _portalService.RegistarEvento("estado", $"Estado atualizado para {novoEstado}", codigoJogo);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError($"Exceção ao atualizar estado do jogo {codigoJogo}: {ex.Message}");
            return false;
        }
    }

    private void AddAuthHeaders(HttpRequestMessage request)
    {
        request.Headers.Add("X-Client-Id", _clientId);

        if (!string.IsNullOrWhiteSpace(_apiKey))
        {
            request.Headers.Add("X-API-Key", _apiKey);
        }
    }

    private async Task<HttpResponseMessage> SendWithRetryAsync(Func<HttpRequestMessage> requestFactory)
    {
        return await _retryPolicy.ExecuteAsync(async () =>
        {
            using var request = requestFactory();
            return await _httpClient.SendAsync(request);
        });
    }

    private static bool IsTransientStatusCode(HttpStatusCode statusCode)
    {
        return statusCode == HttpStatusCode.RequestTimeout
               || statusCode == HttpStatusCode.TooManyRequests
               || (int)statusCode >= 500;
    }

    private static string Truncar(string? valor)
    {
        if (string.IsNullOrWhiteSpace(valor))
        {
            return string.Empty;
        }

        const int maxLen = 300;
        return valor.Length <= maxLen ? valor : valor[..maxLen] + "...";
    }

    private static string NomeEstado(int estado)
    {
        return estado switch
        {
            1 => "Agendado",
            2 => "Em Curso",
            3 => "Finalizado",
            4 => "Cancelado",
            5 => "Adiado",
            _ => $"Estado {estado}"
        };
    }

}

