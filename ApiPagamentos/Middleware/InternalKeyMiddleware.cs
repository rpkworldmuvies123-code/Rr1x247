namespace ApiPagamentos.Middleware;

public class InternalKeyMiddleware(RequestDelegate next, IConfiguration configuration)
{
    private const string HeaderName = "X-Internal-Key";

    public async Task InvokeAsync(HttpContext context)
    {
        // Allow OpenAPI/Scalar endpoints without key
        if (context.Request.Path.StartsWithSegments("/openapi") ||
            context.Request.Path.StartsWithSegments("/scalar"))
        {
            await next(context);
            return;
        }

        var expectedKey = configuration["InternalKey"];
        if (string.IsNullOrEmpty(expectedKey))
        {
            context.Response.StatusCode = 500;
            await context.Response.WriteAsJsonAsync(new { message = "InternalKey nao configurada." });
            return;
        }

        if (!context.Request.Headers.TryGetValue(HeaderName, out var providedKey) ||
            !string.Equals(providedKey, expectedKey, StringComparison.Ordinal))
        {
            context.Response.StatusCode = 401;
            await context.Response.WriteAsJsonAsync(new { message = "Chave de autenticacao interna invalida." });
            return;
        }

        await next(context);
    }
}
