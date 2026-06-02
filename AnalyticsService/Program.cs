using AnalyticsService.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(8080));

builder.Services.AddSingleton<MetricsStore>();
builder.Services.AddSingleton<AlertService>();
builder.Services.AddHostedService<KafkaAnalyticsConsumer>();
builder.Services.AddHostedService<PersistenceWorker>();
builder.Services.AddControllers();

builder.Services.AddHealthChecks()
    .AddCheck<AnalyticsService.KafkaHealthCheck>("kafka", tags: ["messaging"])
    .AddCheck<AnalyticsService.MetricsStoreHealthCheck>("metricsstore", tags: ["internal"]);

var app = builder.Build();
app.MapControllers();

app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    ResponseWriter = async (ctx, report) =>
    {
        ctx.Response.ContentType = "application/json";
        await ctx.Response.WriteAsJsonAsync(new
        {
            status   = report.Status.ToString(),
            duration = report.TotalDuration.TotalMilliseconds,
            checks   = report.Entries.ToDictionary(
                e => e.Key,
                e => (object)new
                {
                    status      = e.Value.Status.ToString(),
                    description = e.Value.Description,
                    duration    = e.Value.Duration.TotalMilliseconds,
                    data        = e.Value.Data,
                    error       = e.Value.Exception?.Message,
                })
        });
    }
});

app.Run();
