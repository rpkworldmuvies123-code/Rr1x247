using ApiResultados.Data;
using ApiResultados.Security;
using DotNetEnv;
using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);

LoadDotEnv(builder.Environment.ContentRootPath);

// Add services to the container.

builder.Services.AddControllers();
// Learn more about configuring OpenAPI at https://aka.ms/aspnet/openapi
builder.Services.AddOpenApi();

var connectionString = BuildConnectionString();
var apiKey = RequireEnv("RESULTADOS_API_KEY");

builder.Services.AddDbContext<ResultadosDbContext>(options =>
    options.UseSqlServer(connectionString));
builder.Services.AddHostedService<SchemaValidationHostedService>();
builder.Services.AddSingleton(new ApiKeySettings { Value = apiKey });

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.MapOpenApi();
    app.MapScalarApiReference();
}

if (!app.Environment.IsDevelopment())
{
    app.UseHsts();
}

app.Use(async (context, next) =>
{
    context.Response.Headers.TryAdd("X-Content-Type-Options", "nosniff");
    context.Response.Headers.TryAdd("X-Frame-Options", "DENY");
    context.Response.Headers.TryAdd("Referrer-Policy", "no-referrer");
    context.Response.Headers.TryAdd("Permissions-Policy", "camera=(), microphone=(), geolocation=()");

    await next();
});

app.UseMiddleware<ApiKeyAuditMiddleware>();
app.UseAuthorization();

app.MapControllers();

app.Run();

return;

static void LoadDotEnv(string contentRootPath)
{
    var envPath = Path.GetFullPath(Path.Combine(contentRootPath, "..", ".env"));
    if (File.Exists(envPath))
    {
        Env.Load(envPath);
    }
}

static string BuildConnectionString()
{
    var host = RequireEnv("RESULTADOS_DB_HOST");
    var portText = RequireEnv("RESULTADOS_DB_PORT");
    var databaseName = RequireEnv("RESULTADOS_DB_NAME");
    var user = RequireEnv("RESULTADOS_DB_USER");
    var password = RequireEnv("RESULTADOS_DB_PASSWORD");

    if (!int.TryParse(portText, out var port))
    {
        throw new InvalidOperationException($"Database port invalida: '{portText}'.");
    }

    var builder = new SqlConnectionStringBuilder
    {
        DataSource = $"{host},{port}",
        InitialCatalog = databaseName,
        UserID = user,
        Password = password,
        Encrypt = true,
        TrustServerCertificate = true,
        ConnectTimeout = 10
    };

    return builder.ConnectionString;
}

static string RequireEnv(string key)
{
    var value = Environment.GetEnvironmentVariable(key);
    if (string.IsNullOrWhiteSpace(value))
    {
        throw new InvalidOperationException($"A variavel de ambiente '{key}' nao esta definida.");
    }

    return value;
}
