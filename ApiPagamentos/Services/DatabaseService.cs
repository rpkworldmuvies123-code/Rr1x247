using System.Data;
using Microsoft.Data.SqlClient;

namespace ApiPagamentos.Services;

public class DomainException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class DatabaseService(IConfiguration configuration)
{
    private SqlConnection CreateConnection()
    {
        var cs = configuration.GetConnectionString("Pagamentos")
                 ?? throw new InvalidOperationException("ConnectionStrings:Pagamentos não configurada.");
        return new SqlConnection(cs);
    }

    private static void MapSqlError(SqlException ex)
    {
        var statusCode = ex.Number switch
        {
            50101 => 404, // registo de saldo nao encontrado
            50102 => 422, // saldo insuficiente
            50103 => 400, // tipo invalido
            50104 => 400, // valor invalido
            _     => 500
        };
        throw new DomainException(ex.Message, statusCode);
    }

    public async Task CriarSaldoUtilizadorAsync(string utilizadorId)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_CriarSaldoUtilizador", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@UtilizadorId", utilizadorId);
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (SqlException ex) { MapSqlError(ex); }
    }

    public async Task<Dictionary<string, object?>> ConsultarSaldoAsync(string utilizadorId)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_ConsultarSaldo", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@UtilizadorId", utilizadorId);
        try
        {
            var rows = await ReadRowsAsync(cmd);
            if (rows.Count == 0) throw new DomainException("Saldo não encontrado para o utilizador.", 404);
            return rows[0];
        }
        catch (SqlException ex) { MapSqlError(ex); throw; }
    }

    public async Task<List<Dictionary<string, object?>>> ConsultarTodosSaldosAsync()
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_ConsultarTodosSaldos", con) { CommandType = CommandType.StoredProcedure };
        return await ReadRowsAsync(cmd);
    }

    public async Task RegistarTransacaoAsync(string utilizadorId, string tipo, decimal valor, int? apostaId = null)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_RegistarTransacao", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@UtilizadorId", utilizadorId);
        cmd.Parameters.AddWithValue("@Tipo", tipo);
        cmd.Parameters.AddWithValue("@Valor", valor);
        cmd.Parameters.AddWithValue("@ApostaId", (object?)apostaId ?? DBNull.Value);
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (SqlException ex) { MapSqlError(ex); }
    }

    public async Task<List<Dictionary<string, object?>>> ConsultarTransacoesAsync(
        string? utilizadorId = null, string? tipo = null, int? apostaId = null,
        DateTime? dataInicio = null, DateTime? dataFim = null, int pagina = 1)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_ConsultarTransacoes", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@UtilizadorId", (object?)utilizadorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Tipo", (object?)tipo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@ApostaId", (object?)apostaId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DataInicio", (object?)dataInicio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DataFim", (object?)dataFim ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Pagina", pagina);
        return await ReadRowsAsync(cmd);
    }

    private static async Task<List<Dictionary<string, object?>>> ReadRowsAsync(SqlCommand cmd)
    {
        var rows = new List<Dictionary<string, object?>>();
        await using var reader = await cmd.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var row = new Dictionary<string, object?>();
            for (var i = 0; i < reader.FieldCount; i++)
                row[reader.GetName(i)] = reader.IsDBNull(i) ? null : reader.GetValue(i);
            rows.Add(row);
        }
        return rows;
    }
}
