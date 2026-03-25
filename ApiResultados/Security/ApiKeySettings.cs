namespace ApiResultados.Security;

public sealed class ApiKeySettings
{
    public required string Value { get; init; }
    public string HeaderName { get; init; } = "X-API-Key";
    public string ClientIdHeaderName { get; init; } = "X-Client-Id";
}

