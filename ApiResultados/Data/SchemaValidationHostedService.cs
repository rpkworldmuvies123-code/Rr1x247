using Microsoft.Data.SqlClient;
using Microsoft.EntityFrameworkCore;

namespace ApiResultados.Data;

public sealed class SchemaValidationHostedService(
    IServiceProvider serviceProvider,
    IHostApplicationLifetime applicationLifetime,
    ILogger<SchemaValidationHostedService> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = serviceProvider.CreateScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<ResultadosDbContext>();

        try
        {
            await dbContext.Database.OpenConnectionAsync(cancellationToken);
        }
        catch (SqlException ex)
        {
            FailStartup(BuildReadableDatabaseError(ex));
            return;
        }

        try
        {
            await using var command = dbContext.Database.GetDbConnection().CreateCommand();
            command.CommandText = @"
                SELECT t.name
                FROM sys.tables AS t
                INNER JOIN sys.schemas AS s ON s.schema_id = t.schema_id
                WHERE s.name = 'dbo' AND t.name IN ('jogo', 'resultado');";

            var existingTables = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            await using var reader = await command.ExecuteReaderAsync(cancellationToken);
            while (await reader.ReadAsync(cancellationToken))
            {
                existingTables.Add(reader.GetString(0));
            }

            var missingTables = new[] { "jogo", "resultado" }
                .Where(table => !existingTables.Contains(table))
                .ToArray();

            if (missingTables.Length > 0)
            {
                FailStartup($"A base de dados nao tem as tabelas obrigatorias: {string.Join(", ", missingTables)}.");
            }
        }
        finally
        {
            await dbContext.Database.CloseConnectionAsync();
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;

    private void FailStartup(string message)
    {
        logger.LogCritical("{Message}", message);
        Environment.ExitCode = 1;
        applicationLifetime.StopApplication();
    }

    private static string BuildReadableDatabaseError(SqlException ex)
    {
        return ex.Number switch
        {
            18456 => "Falha de autenticacao na base de dados para o utilizador configurado. " +
                     "Confirma RESULTADOS_DB_USER/RESULTADOS_DB_PASSWORD no .env e executa SQL/credentials.sql + SQL/setup.sql antes de arrancar a API.",
            4060 => "Nao foi possivel abrir a base de dados configurada. " +
                    "Confirma RESULTADOS_DB_NAME no .env e executa SQL/setup.sql antes de arrancar a API.",
            53 or 11001 or 10060 => "Nao foi possivel ligar ao servidor SQL. " +
                                     "Confirma RESULTADOS_DB_HOST/RESULTADOS_DB_PORT no .env e se o container do SQL Server esta a correr.",
            -2 => "Timeout ao ligar a base de dados. " +
                  "Confirma se o SQL Server terminou o arranque e se a porta configurada esta correta.",
            40613 => "A base de dados esta temporariamente indisponivel. " +
                     "Tenta novamente daqui a instantes apos validar o estado do SQL Server.",
            _ => $"Falha ao ligar a base de dados (SQL {ex.Number}). Mensagem original: {ex.Message}"
        };
    }
}
