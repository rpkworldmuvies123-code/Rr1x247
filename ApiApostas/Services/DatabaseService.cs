using System.Data;
using Microsoft.Data.SqlClient;

namespace ApiApostas.Services;

public class DomainException(string message, int statusCode = 400) : Exception(message)
{
    public int StatusCode { get; } = statusCode;
}

public class DatabaseService(IConfiguration configuration)
{
    private SqlConnection CreateConnection()
    {
        var cs = configuration.GetConnectionString("Apostas")
                 ?? throw new InvalidOperationException("ConnectionStrings:Apostas não configurada.");
        return new SqlConnection(cs);
    }

    private static void MapSqlError(SqlException ex)
    {
        var msg = ex.Message;
        var num = ex.Number;
        var statusCode = num switch
        {
            50001 => 400, // formato de código inválido
            50002 => 409, // código de jogo duplicado
            50003 => 404, // jogo não encontrado
            50004 => 409, // jogo em estado terminal
            50005 => 409,
            50006 => 400,
            50007 => 400,
            50008 => 400,
            50009 => 400,
            50010 => 400,
            50011 => 400,
            50012 => 422,
            50013 => 400,
            50014 => 404,
            50015 => 409,
            50016 => 403,
            50017 => 409, // resultado duplicado
            50018 => 400,
            50019 => 409, // jogo tem apostas associadas
            50020 => 400,
            50021 => 404, // utilizador não encontrado (admin SPs)
            _ => 500
        };
        throw new DomainException(msg, statusCode);
    }

    // ──────────────────────────────────────────────
    // JOGOS
    // ──────────────────────────────────────────────

    public async Task<int> InserirJogoAsync(string codigoJogo, DateTime dataHoraInicio,
        string equipaCasa, string equipaFora, string tipoCompeticao)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_InserirJogo", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@CodigoJogo", codigoJogo);
        cmd.Parameters.AddWithValue("@DataHoraInicio", dataHoraInicio);
        cmd.Parameters.AddWithValue("@EquipaCasa", equipaCasa);
        cmd.Parameters.AddWithValue("@EquipaFora", equipaFora);
        cmd.Parameters.AddWithValue("@TipoCompeticao", tipoCompeticao);
        var idParam = cmd.Parameters.Add("@Id", SqlDbType.Int);
        idParam.Direction = ParameterDirection.Output;
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (SqlException ex) { MapSqlError(ex); }
        return (int)idParam.Value;
    }

    public async Task AtualizarJogoAsync(string codigoJogo, int? estado = null,
        int? golosCasa = null, int? golosVisitante = null)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_AtualizarJogo", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@CodigoJogo", codigoJogo);
        cmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GolosCasa", (object?)golosCasa ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@GolosVisitante", (object?)golosVisitante ?? DBNull.Value);
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (SqlException ex) { MapSqlError(ex); }
    }

    public async Task RemoverJogoAsync(string codigoJogo)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_RemoverJogo", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@CodigoJogo", codigoJogo);
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (SqlException ex) { MapSqlError(ex); }
    }

    public async Task<List<Dictionary<string, object?>>> ConsultarJogosAsync(
        DateTime? data = null, int? estado = null, string? tipoCompeticao = null)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_ConsultarJogos", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@Data", (object?)data ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@TipoCompeticao", (object?)tipoCompeticao ?? DBNull.Value);
        return await ReadRowsAsync(cmd);
    }

    public async Task<Dictionary<string, object?>> ConsultarJogoAsync(string codigoJogo)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_ConsultarJogo", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@CodigoJogo", codigoJogo);
        try
        {
            var rows = await ReadRowsAsync(cmd);
            if (rows.Count == 0) throw new DomainException($"Jogo '{codigoJogo}' não encontrado.", 404);
            return rows[0];
        }
        catch (SqlException ex) { MapSqlError(ex); throw; }
    }

    // ──────────────────────────────────────────────
    // APOSTAS
    // ──────────────────────────────────────────────

    public async Task<(int ApostaId, decimal OddMomento)> InserirApostaAsync(string codigoJogo, string utilizadorId,
        string tipoAposta, decimal montante)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_InserirAposta", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@CodigoJogo", codigoJogo);
        cmd.Parameters.AddWithValue("@UtilizadorId", utilizadorId);
        cmd.Parameters.AddWithValue("@TipoAposta", tipoAposta);
        cmd.Parameters.AddWithValue("@MontanteApostado", montante);
        var idParam = cmd.Parameters.Add("@ApostaId", SqlDbType.Int);
        idParam.Direction = ParameterDirection.Output;
        var oddParam = cmd.Parameters.Add("@OddMomento", SqlDbType.Decimal);
        oddParam.Direction = ParameterDirection.Output;
        oddParam.Precision = 10; oddParam.Scale = 4;
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (SqlException ex) { MapSqlError(ex); }
        return ((int)idParam.Value, (decimal)oddParam.Value);
    }

    public async Task RemoverApostaAsync(int apostaId)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_RemoverAposta", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@ApostaId", apostaId);
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (SqlException ex) { MapSqlError(ex); }
    }

    public async Task<List<Dictionary<string, object?>>> ObterPagamentosPendentesAsync(string? codigoJogo = null)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_ObterPagamentosPendentes", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@CodigoJogo", (object?)codigoJogo ?? DBNull.Value);
        return await ReadRowsAsync(cmd);
    }

    public async Task MarcarPagamentoProcessadoAsync(int apostaId)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_MarcarPagamentoProcessado", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@ApostaId", apostaId);
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (SqlException ex) { MapSqlError(ex); }
    }

    public async Task<Dictionary<string, object?>> ConsultarOddsJogoAsync(string codigoJogo)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_ConsultarOddsJogo", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@CodigoJogo", codigoJogo);
        try
        {
            var rows = await ReadRowsAsync(cmd);
            if (rows.Count == 0) throw new DomainException($"Jogo '{codigoJogo}' não encontrado.", 404);
            return rows[0];
        }
        catch (SqlException ex) { MapSqlError(ex); throw; }
    }

    public async Task CancelarApostaAsync(int apostaId, string utilizadorId, bool isAdmin = false)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_CancelarAposta", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@ApostaId", apostaId);
        cmd.Parameters.AddWithValue("@UtilizadorId", utilizadorId);
        cmd.Parameters.AddWithValue("@IsAdmin", isAdmin ? 1 : 0);
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (SqlException ex) { MapSqlError(ex); }
    }

    public async Task<List<Dictionary<string, object?>>> ConsultarApostasAsync(
        string? utilizadorId = null, string? codigoJogo = null,
        int? estado = null, DateTime? dataInicio = null, DateTime? dataFim = null)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_ConsultarApostas", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@UtilizadorId", (object?)utilizadorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@CodigoJogo", (object?)codigoJogo ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Estado", (object?)estado ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DataInicio", (object?)dataInicio ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@DataFim", (object?)dataFim ?? DBNull.Value);
        return await ReadRowsAsync(cmd);
    }

    public async Task<Dictionary<string, object?>> ConsultarApostaAsync(
        int apostaId, string? utilizadorId = null, bool isAdmin = false)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_ConsultarAposta", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@ApostaId", apostaId);
        cmd.Parameters.AddWithValue("@UtilizadorId", (object?)utilizadorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@IsAdmin", isAdmin ? 1 : 0);
        try
        {
            var rows = await ReadRowsAsync(cmd);
            if (rows.Count == 0) throw new DomainException($"Aposta {apostaId} não encontrada.", 404);
            return rows[0];
        }
        catch (SqlException ex) { MapSqlError(ex); throw; }
    }

    // ──────────────────────────────────────────────
    // RESULTADOS
    // ──────────────────────────────────────────────

    public async Task<int> InserirResultadoAsync(string codigoJogo, int golosCasa, int golosVisitante)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_InserirResultado", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@CodigoJogo", codigoJogo);
        cmd.Parameters.AddWithValue("@GolosCasa", golosCasa);
        cmd.Parameters.AddWithValue("@GolosVisitante", golosVisitante);
        var idParam = cmd.Parameters.Add("@Id", SqlDbType.Int);
        idParam.Direction = ParameterDirection.Output;
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (SqlException ex) { MapSqlError(ex); }
        return (int)idParam.Value;
    }

    public async Task<Dictionary<string, object?>> ConsultarResultadoJogoAsync(string codigoJogo)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_ConsultarResultadoJogo", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@CodigoJogo", codigoJogo);
        try
        {
            var rows = await ReadRowsAsync(cmd);
            if (rows.Count == 0) throw new DomainException($"Resultado para '{codigoJogo}' não encontrado.", 404);
            return rows[0];
        }
        catch (SqlException ex) { MapSqlError(ex); throw; }
    }

    // ──────────────────────────────────────────────
    // ESTATÍSTICAS
    // ──────────────────────────────────────────────

    public async Task<Dictionary<string, object?>> EstatisticasJogoAsync(string codigoJogo)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_EstatisticasJogo", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@CodigoJogo", codigoJogo);
        try
        {
            var rows = await ReadRowsAsync(cmd);
            if (rows.Count == 0) throw new DomainException($"Jogo '{codigoJogo}' não encontrado.", 404);
            return rows[0];
        }
        catch (SqlException ex) { MapSqlError(ex); throw; }
    }

    public async Task<Dictionary<string, object?>> EstatisticasCompeticaoAsync(string tipoCompeticao)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_EstatisticasCompeticao", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@TipoCompeticao", tipoCompeticao);
        try
        {
            var rows = await ReadRowsAsync(cmd);
            if (rows.Count == 0) throw new DomainException($"Competição '{tipoCompeticao}' não encontrada.", 404);
            return rows[0];
        }
        catch (SqlException ex) { MapSqlError(ex); throw; }
    }

    // ──────────────────────────────────────────────
    // ADMIN (sem operações financeiras — delegadas a ApiPagamentos)
    // ──────────────────────────────────────────────

    public async Task<List<Dictionary<string, object?>>> ConsultarUtilizadoresAsync()
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_ConsultarUtilizadores", con) { CommandType = CommandType.StoredProcedure };
        return await ReadRowsAsync(cmd);
    }

    public async Task LogarAcaoAdminAsync(string adminId, string? utilizadorId, string acao, string? detalhe)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_LogarAcaoAdmin", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@AdminId", adminId);
        cmd.Parameters.AddWithValue("@UtilizadorId", (object?)utilizadorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Acao", acao);
        cmd.Parameters.AddWithValue("@Detalhe", (object?)detalhe ?? DBNull.Value);
        try { await cmd.ExecuteNonQueryAsync(); }
        catch (SqlException ex) { MapSqlError(ex); }
    }

    public async Task<List<Dictionary<string, object?>>> ConsultarLogAdminAsync(
        string? adminId = null, string? utilizadorId = null, int? pagina = null)
    {
        await using var con = CreateConnection();
        await con.OpenAsync();
        await using var cmd = new SqlCommand("sp_ConsultarLogAdmin", con) { CommandType = CommandType.StoredProcedure };
        cmd.Parameters.AddWithValue("@AdminId", (object?)adminId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@UtilizadorId", (object?)utilizadorId ?? DBNull.Value);
        cmd.Parameters.AddWithValue("@Pagina", (object?)pagina ?? DBNull.Value);
        return await ReadRowsAsync(cmd);
    }

    // ──────────────────────────────────────────────
    // HELPER
    // ──────────────────────────────────────────────

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
