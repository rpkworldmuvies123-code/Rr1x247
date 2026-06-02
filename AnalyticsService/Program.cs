using AnalyticsService.Services;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(8080));

builder.Services.AddSingleton<MetricsStore>();
builder.Services.AddSingleton<AlertService>();
builder.Services.AddHostedService<KafkaAnalyticsConsumer>();
builder.Services.AddHostedService<PersistenceWorker>();
builder.Services.AddControllers();

var app = builder.Build();
app.MapControllers();
app.Run();
