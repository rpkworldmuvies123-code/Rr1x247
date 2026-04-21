using GeradorDados.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(options => { options.ListenAnyIP(8080); });

builder.Logging.ClearProviders();
builder.Logging.AddConsole();
builder.Logging.AddConfiguration(builder.Configuration.GetSection("Logging"));

builder.Services.AddSingleton<EquipasService>();
builder.Services.AddSingleton<MatchmakingService>();
builder.Services.AddScoped<HttpClientService>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<HttpClientService>>();
    var portalService = provider.GetRequiredService<SimulacaoPortalService>();
    var baseUrl = builder.Configuration["ApiResultados:BaseUrl"] ?? "http://localhost:5204";
    var apiKey = builder.Configuration["ApiResultados:ApiKey"];
    var clientId = builder.Configuration["ApiResultados:ClientId"] ?? "gerador-dados";
    return new HttpClientService(baseUrl, logger, apiKey, clientId, portalService);
});
builder.Services.AddScoped<OrquestradorJornada>();
builder.Services.AddSingleton<SimulacaoPortalService>(provider =>
{
    var scopeFactory = provider.GetRequiredService<IServiceScopeFactory>();
    var logger = provider.GetRequiredService<ILogger<SimulacaoPortalService>>();
    var maxEventos = LerInteiroPositivo(builder.Configuration["Simulation:MaxEventos"], 1000);
    return new SimulacaoPortalService(scopeFactory, logger, maxEventos);
});

var app = builder.Build();

var logger = app.Services.GetRequiredService<ILogger<Program>>();
var portalService = app.Services.GetRequiredService<SimulacaoPortalService>();

var caminhoEquipas = Path.Combine(app.Environment.ContentRootPath, "Data", "equipas.json");
var intervaloDefault = LerInteiroPositivo(app.Configuration["Simulation:IntervaloAtualizacaoSegundos"], 10);

logger.LogInformation("GeradorDados Web iniciado com sucesso.");
logger.LogInformation("API Resultados: {BaseUrl}", app.Configuration["ApiResultados:BaseUrl"]);
logger.LogInformation("Caminho equipas: {CaminhoEquipas}", caminhoEquipas);


app.UseDefaultFiles();
app.UseStaticFiles(new StaticFileOptions
{
    OnPrepareResponse = context =>
    {
        context.Context.Response.Headers.CacheControl = "no-store, no-cache, must-revalidate";
        context.Context.Response.Headers.Pragma = "no-cache";
        context.Context.Response.Headers.Expires = "0";
    }
});

app.MapGet("/api/estado", () => Results.Ok(portalService.ObterEstado()));

app.MapGet("/api/config", () => Results.Ok(new
{
    intervaloAtualizacaoSegundos = intervaloDefault,
    apiResultadosBaseUrl = app.Configuration["ApiResultados:BaseUrl"]
}));

app.MapPost("/api/simulacao/iniciar", (IniciarSimulacaoRequest? request) =>
{
    var intervalo = request?.IntervaloAtualizacaoSegundos ?? intervaloDefault;
    if (intervalo < 1)
    {
        return Results.BadRequest(new { message = "intervaloAtualizacaoSegundos deve ser >= 1." });
    }

    var iniciou = portalService.TentarIniciarExecucao(caminhoEquipas, intervalo);
    if (!iniciou)
    {
        return Results.Conflict(new { message = "Ja existe uma execucao em curso." });
    }

    return Results.Accepted("/api/estado", new { message = "Execucao iniciada.", intervaloAtualizacaoSegundos = intervalo });
});

app.MapPost("/api/estado/limpar", () =>
{
    var limpou = portalService.TentarLimparHistorico(out var mensagem);
    if (!limpou)
    {
        return Results.Conflict(new { message = mensagem });
    }

    return Results.Ok(new { message = mensagem });
});

app.Run();

return;

static int LerInteiroPositivo(string? valor, int fallback)
{
    if (int.TryParse(valor, out var parsed) && parsed >= 1)
    {
        return parsed;
    }

    return fallback;
}

public sealed class IniciarSimulacaoRequest
{
    public int? IntervaloAtualizacaoSegundos { get; set; }
}

