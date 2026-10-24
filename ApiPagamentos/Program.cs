using ApiPagamentos.Middleware;
using ApiPagamentos.Services;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(8080));

builder.Services.AddControllers();
builder.Services.AddOpenApi();
builder.Services.AddScoped<DatabaseService>();

var app = builder.Build();

// Global error handler
app.UseExceptionHandler(errApp => errApp.Run(async ctx =>
{
    var ex = ctx.Features.Get<Microsoft.AspNetCore.Diagnostics.IExceptionHandlerFeature>()?.Error;
    var (status, msg) = ex switch
    {
        DomainException de => (de.StatusCode, de.Message),
        _ => (500, "Erro interno do servidor.")
    };
    ctx.Response.StatusCode = status;
    ctx.Response.ContentType = "application/json";
    await ctx.Response.WriteAsJsonAsync(new { message = msg });
}));

app.MapOpenApi();
app.MapScalarApiReference(opts => opts.WithTitle("RR1X24 — API Pagamentos"));

// All routes require valid X-Internal-Key
app.UseMiddleware<InternalKeyMiddleware>();

app.MapControllers();

app.Run();
