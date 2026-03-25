using GeradorDados.Services;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

// Configurar construtor de serviços e logging
var serviceCollection = new ServiceCollection();

// Carregar configurações
var configuration = new ConfigurationBuilder()
    .SetBasePath(Directory.GetCurrentDirectory())
    .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
    .Build();

// Adicionar logging
serviceCollection.AddLogging(builder =>
{
    builder.AddConsole();
    builder.AddConfiguration(configuration.GetSection("Logging"));
});

// Registar serviços
serviceCollection.AddSingleton<EquipasService>();
serviceCollection.AddSingleton<MatchmakingService>();
serviceCollection.AddScoped<HttpClientService>(provider =>
{
    var logger = provider.GetRequiredService<ILogger<HttpClientService>>();
    var baseUrl = configuration["ApiResultados:BaseUrl"] ?? "http://localhost:5001";
    return new HttpClientService(baseUrl, logger);
});
serviceCollection.AddScoped<OrquestradorJornada>();

// Construir service provider
var serviceProvider = serviceCollection.BuildServiceProvider();

// Obter logger e orquestrador
var logger = serviceProvider.GetRequiredService<ILogger<Program>>();
var orquestrador = serviceProvider.GetRequiredService<OrquestradorJornada>();

try
{
    logger.LogInformation("GeradorDados iniciado com sucesso!");
    logger.LogInformation("Configuração carregada:");
    logger.LogInformation($"  API URL: {configuration["ApiResultados:BaseUrl"]}");

    // Determinar caminho para ficheiro de equipas
    string caminhoEquipas = Path.Combine(
        Directory.GetCurrentDirectory(),
        "Data",
        "equipas.json");

    logger.LogInformation($"  Caminho de equipas: {caminhoEquipas}");

    // Executar simulação da Jornada 1
    await orquestrador.ExecutarJornadaAsync(caminhoEquipas);
}
catch (Exception ex)
{
    logger.LogError($"Erro fatal na aplicação: {ex.Message}");
    logger.LogError($"Stack trace: {ex.StackTrace}");
    Environment.Exit(1);
}
