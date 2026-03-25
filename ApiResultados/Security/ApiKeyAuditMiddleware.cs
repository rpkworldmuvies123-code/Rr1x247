using System.Diagnostics;
using System.Security.Cryptography;
using System.Text;

namespace ApiResultados.Security;

public sealed class ApiKeyAuditMiddleware(RequestDelegate next, ApiKeySettings settings, ILogger<ApiKeyAuditMiddleware> logger)
{
    public async Task InvokeAsync(HttpContext context)
    {
        if (!IsProtectedWriteOperation(context.Request))
        {
            await next(context);
            return;
        }

        var clientId = context.Request.Headers[settings.ClientIdHeaderName].ToString();
        if (string.IsNullOrWhiteSpace(clientId))
        {
            clientId = "internal-unknown";
        }

        var remoteIp = context.Connection.RemoteIpAddress?.ToString() ?? "ip-indisponivel";
        var providedApiKey = context.Request.Headers[settings.HeaderName].ToString();

        if (string.IsNullOrWhiteSpace(providedApiKey))
        {
            logger.LogWarning(
                "AUDIT denied sem API key. ClientId={ClientId} Ip={RemoteIp} Metodo={Method} Rota={Path} ApiKeyFingerprint={ApiKeyFingerprint}",
                clientId,
                remoteIp,
                context.Request.Method,
                context.Request.Path,
                "missing");

            await WriteError(context, StatusCodes.Status401Unauthorized,
                $"API key em falta. Envie o header {settings.HeaderName}.");
            return;
        }

        if (!IsSameApiKey(providedApiKey, settings.Value))
        {
            logger.LogWarning(
                "AUDIT denied API key invalida. ClientId={ClientId} Ip={RemoteIp} Metodo={Method} Rota={Path} ApiKeyFingerprint={ApiKeyFingerprint}",
                clientId,
                remoteIp,
                context.Request.Method,
                context.Request.Path,
                BuildApiKeyFingerprint(providedApiKey));

            await WriteError(context, StatusCodes.Status403Forbidden,
                "API key invalida para operacoes de escrita.");
            return;
        }

        var sw = Stopwatch.StartNew();
        await next(context);
        sw.Stop();

        logger.LogInformation(
            "AUDIT ok. ClientId={ClientId} Ip={RemoteIp} Metodo={Method} Rota={Path} Status={StatusCode} DuracaoMs={ElapsedMs} ApiKeyFingerprint={ApiKeyFingerprint}",
            clientId,
            remoteIp,
            context.Request.Method,
            context.Request.Path,
            context.Response.StatusCode,
            sw.ElapsedMilliseconds,
            BuildApiKeyFingerprint(providedApiKey));
    }

    private static bool IsProtectedWriteOperation(HttpRequest request)
    {
        if (request.Path.Equals("/health/stats", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        if (!request.Path.StartsWithSegments("/api/jogos", StringComparison.OrdinalIgnoreCase))
        {
            return false;
        }

        return HttpMethods.IsPost(request.Method)
               || HttpMethods.IsPatch(request.Method)
               || HttpMethods.IsPut(request.Method)
               || HttpMethods.IsDelete(request.Method);
    }

    private static bool IsSameApiKey(string providedApiKey, string configuredApiKey)
    {
        var providedBytes = Encoding.UTF8.GetBytes(providedApiKey);
        var configuredBytes = Encoding.UTF8.GetBytes(configuredApiKey);

        return CryptographicOperations.FixedTimeEquals(providedBytes, configuredBytes);
    }

    private static async Task WriteError(HttpContext context, int statusCode, string message)
    {
        context.Response.StatusCode = statusCode;
        context.Response.ContentType = "application/json";
        await context.Response.WriteAsJsonAsync(new { message });
    }

    private static string BuildApiKeyFingerprint(string apiKey)
    {
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return "missing";
        }

        var prefixLen = Math.Min(4, apiKey.Length);
        var prefix = apiKey[..prefixLen];

        var hashBytes = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
        var hashHex = Convert.ToHexString(hashBytes)[..8].ToLowerInvariant();

        return $"{prefix}...#{hashHex}";
    }
}
