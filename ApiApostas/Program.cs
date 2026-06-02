using System.Text;
using ApiApostas.Data;
using ApiApostas.Models;
using ApiApostas.Services;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Scalar.AspNetCore;

var builder = WebApplication.CreateBuilder(args);
builder.WebHost.ConfigureKestrel(o => o.ListenAnyIP(8080));

// EF + Identity
builder.Services.AddDbContext<ApostasDbContext>(options =>
    options.UseSqlServer(builder.Configuration.GetConnectionString("Apostas")));

builder.Services.AddIdentity<ApplicationUser, IdentityRole>(options =>
    {
        options.Password.RequireDigit = false;
        options.Password.RequiredLength = 6;
        options.Password.RequireNonAlphanumeric = false;
        options.Password.RequireUppercase = false;
        options.Password.RequireLowercase = false;
    })
    .AddEntityFrameworkStores<ApostasDbContext>()
    .AddDefaultTokenProviders();

// JWT
var jwtSecret = builder.Configuration["Jwt:Secret"]
                ?? throw new InvalidOperationException("Jwt:Secret não configurado.");

builder.Services.AddAuthentication(options =>
    {
        options.DefaultAuthenticateScheme = JwtBearerDefaults.AuthenticationScheme;
        options.DefaultChallengeScheme = JwtBearerDefaults.AuthenticationScheme;
    })
    .AddJwtBearer(options =>
    {
        options.TokenValidationParameters = new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidateAudience = true,
            ValidateLifetime = true,
            ValidateIssuerSigningKey = true,
            ValidIssuer = builder.Configuration["Jwt:Issuer"],
            ValidAudience = builder.Configuration["Jwt:Audience"],
            IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwtSecret))
        };
    });

builder.Services.AddAuthorization();
builder.Services.AddControllers()
    .AddJsonOptions(opts => opts.JsonSerializerOptions.PropertyNamingPolicy = null);
builder.Services.AddOpenApi();
builder.Services.AddScoped<JwtService>();
builder.Services.AddScoped<DatabaseService>();
builder.Services.AddScoped<ApiApostas.Controllers.JogosController>();

builder.Services.AddSingleton<KafkaProducerService>();

// RabbitMQ — singleton criado de forma assíncrona antes de arrancar o host
builder.Services.AddSingleton(sp =>
    RabbitMqService.CreateAsync(
        sp.GetRequiredService<IConfiguration>(),
        sp.GetRequiredService<ILogger<RabbitMqService>>()).GetAwaiter().GetResult());

builder.Services.AddHostedService<KafkaConsumerService>();
builder.Services.AddHostedService<BetResolutionWorker>();
builder.Services.AddHostedService<PendingBetsSweeperService>();
// SincronizacaoService (polling HTTP) substituído pelo KafkaConsumerService

// PagamentosClient — typed HttpClient com InternalKey injectado no header
builder.Services.AddHttpClient<PagamentosClient>((sp, client) =>
{
    var config = sp.GetRequiredService<IConfiguration>();
    var baseUrl = config["ApiPagamentos:BaseUrl"]
                  ?? throw new InvalidOperationException("ApiPagamentos:BaseUrl não configurado.");
    var key = config["ApiPagamentos:InternalKey"]
              ?? throw new InvalidOperationException("ApiPagamentos:InternalKey não configurado.");
    client.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
    client.DefaultRequestHeaders.Add("X-Internal-Key", key);
});

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
app.MapScalarApiReference(opts => opts.WithTitle("BetStrike — API Apostas"));

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();

// Seed: roles + admin user (schema gerido por SQL/apostas_setup.sql)
await using (var scope = app.Services.CreateAsyncScope())
{
    var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();
    foreach (var role in new[] { "Admin", "User" })
        if (!await roleManager.RoleExistsAsync(role))
            await roleManager.CreateAsync(new IdentityRole(role));

    var userManager = scope.ServiceProvider.GetRequiredService<UserManager<ApplicationUser>>();
    var adminEmail = app.Configuration["Admin:Email"] ?? "admin@betstrike.pt";
    var adminPassword = app.Configuration["Admin:Password"] ?? "Admin123!";

    if (await userManager.FindByEmailAsync(adminEmail) == null)
    {
        var admin = new ApplicationUser
        {
            UserName = adminEmail,
            Email = adminEmail,
            NomeCompleto = "Administrador BetStrike",
            DataRegisto = DateTime.UtcNow
        };
        var result = await userManager.CreateAsync(admin, adminPassword);
        if (result.Succeeded)
            await userManager.AddToRoleAsync(admin, "Admin");
    }
}

app.Run();
