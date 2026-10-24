-- Rodar apostas_credentials.example.sql antes deste ficheiro

USE [master];
GO

IF DB_ID(N'rr1x24-pagamentos') IS NULL
BEGIN
    CREATE DATABASE [rr1x24-pagamentos];
    PRINT 'database rr1x24-pagamentos criada.';
END
ELSE
    PRINT 'database rr1x24-pagamentos ja existe.';
GO

USE [rr1x24-pagamentos];
GO

-- ================================================================
-- TABELAS
-- ================================================================
BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID(N'dbo.Saldo_Utilizador', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Saldo_Utilizador (
            UtilizadorId         NVARCHAR(450) NOT NULL,
            SaldoAtual           DECIMAL(10,2) NOT NULL CONSTRAINT df_saldo_valor DEFAULT (0.00),
            DataHoraAtualizacao  DATETIME2     NOT NULL CONSTRAINT df_saldo_data  DEFAULT (GETUTCDATE()),
            CONSTRAINT PK_Saldo_Utilizador PRIMARY KEY (UtilizadorId),
            CONSTRAINT CK_Saldo_NaoNegativo CHECK (SaldoAtual >= 0)
        );
        PRINT 'tabela Saldo_Utilizador criada.';
    END
    ELSE PRINT 'tabela Saldo_Utilizador ja existe.';

    IF OBJECT_ID(N'dbo.Transacoes', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Transacoes (
            Id               INT IDENTITY(1,1) PRIMARY KEY,
            ApostaId         INT           NULL,
            UtilizadorId     NVARCHAR(450) NOT NULL,
            TipoTransacao    CHAR(2)       NOT NULL,
            Valor            DECIMAL(10,2) NOT NULL,
            DataHora         DATETIME2     NOT NULL CONSTRAINT df_transacao_data  DEFAULT (GETUTCDATE()),
            Estado           NVARCHAR(20)  NOT NULL CONSTRAINT df_transacao_estado DEFAULT ('Processada'),
            CONSTRAINT CK_Transacoes_Tipo   CHECK (TipoTransacao IN ('AP','PG','RE','DE','LV')),
            CONSTRAINT CK_Transacoes_Estado CHECK (Estado IN ('Pendente','Processada','Falhada','Reembolsada'))
        );
        CREATE INDEX IX_Transacoes_UtilizadorId ON dbo.Transacoes (UtilizadorId);
        CREATE INDEX IX_Transacoes_ApostaId     ON dbo.Transacoes (ApostaId) WHERE ApostaId IS NOT NULL;
        PRINT 'tabela Transacoes criada.';
    END
    ELSE PRINT 'tabela Transacoes ja existe.';

    COMMIT TRAN;
    PRINT 'schema rr1x24-pagamentos validado.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'erro tabelas: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ================================================================
-- STORED PROCEDURES
-- Codigos de erro: 50100-50199 (range exclusivo de pagamentos)
-- ================================================================

-- ----------------------------------------------------------------
-- sp_CriarSaldoUtilizador  — chamado por ApiApostas apos registo
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_CriarSaldoUtilizador
    @UtilizadorId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM dbo.Saldo_Utilizador WHERE UtilizadorId = @UtilizadorId)
    BEGIN
        INSERT INTO dbo.Saldo_Utilizador (UtilizadorId, SaldoAtual)
        VALUES (@UtilizadorId, 50.00);

        INSERT INTO dbo.Transacoes (UtilizadorId, TipoTransacao, Valor, Estado)
        VALUES (@UtilizadorId, 'DE', 50.00, 'Processada');

        PRINT 'saldo criado para utilizador ' + @UtilizadorId;
    END

    COMMIT TRANSACTION;
END
GO

-- ----------------------------------------------------------------
-- sp_ConsultarSaldo
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarSaldo
    @UtilizadorId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UtilizadorId, SaldoAtual, DataHoraAtualizacao
    FROM dbo.Saldo_Utilizador
    WHERE UtilizadorId = @UtilizadorId;
END
GO

-- ----------------------------------------------------------------
-- sp_ConsultarTodosSaldos  — para listagem admin (sem cross-DB)
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarTodosSaldos
AS
BEGIN
    SET NOCOUNT ON;
    SELECT UtilizadorId, SaldoAtual, DataHoraAtualizacao
    FROM dbo.Saldo_Utilizador
    ORDER BY DataHoraAtualizacao DESC;
END
GO

-- ----------------------------------------------------------------
-- sp_RegistarTransacao
--   Regista uma transacao e actualiza o saldo.
--   @Tipo: AP/LV => debita; PG/RE/DE => credita
--   @Valor: sempre positivo
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_RegistarTransacao
    @UtilizadorId NVARCHAR(450),
    @Tipo         CHAR(2),
    @Valor        DECIMAL(10,2),
    @ApostaId     INT = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF @Valor <= 0
        THROW 50104, 'O valor da transacao deve ser positivo.', 1;

    IF @Tipo NOT IN ('AP','PG','RE','DE','LV')
        THROW 50103, 'Tipo de transacao invalido. Use AP, PG, RE, DE ou LV.', 1;

    DECLARE @SaldoAtual DECIMAL(10,2);
    SELECT @SaldoAtual = SaldoAtual
    FROM dbo.Saldo_Utilizador WITH (UPDLOCK)
    WHERE UtilizadorId = @UtilizadorId;

    IF @SaldoAtual IS NULL
        THROW 50101, 'Registo de saldo nao encontrado para o utilizador.', 1;

    -- AP e LV debitam
    IF @Tipo IN ('AP', 'LV')
    BEGIN
        IF @SaldoAtual < @Valor
            THROW 50102, 'Saldo insuficiente.', 1;

        UPDATE dbo.Saldo_Utilizador
        SET SaldoAtual = SaldoAtual - @Valor, DataHoraAtualizacao = GETUTCDATE()
        WHERE UtilizadorId = @UtilizadorId;
    END
    ELSE
    BEGIN
        UPDATE dbo.Saldo_Utilizador
        SET SaldoAtual = SaldoAtual + @Valor, DataHoraAtualizacao = GETUTCDATE()
        WHERE UtilizadorId = @UtilizadorId;
    END

    INSERT INTO dbo.Transacoes (ApostaId, UtilizadorId, TipoTransacao, Valor, Estado)
    VALUES (@ApostaId, @UtilizadorId, @Tipo, @Valor, 'Processada');

    COMMIT TRANSACTION;
END
GO

-- ----------------------------------------------------------------
-- sp_ConsultarTransacoes
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarTransacoes
    @UtilizadorId NVARCHAR(450) = NULL,
    @Tipo         CHAR(2)       = NULL,
    @ApostaId     INT           = NULL,
    @DataInicio   DATETIME2     = NULL,
    @DataFim      DATETIME2     = NULL,
    @Pagina       INT           = 1
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Offset INT = (ISNULL(@Pagina, 1) - 1) * 50;

    SELECT Id, ApostaId, UtilizadorId, TipoTransacao, Valor, DataHora, Estado
    FROM dbo.Transacoes
    WHERE (@UtilizadorId IS NULL OR UtilizadorId  = @UtilizadorId)
      AND (@Tipo         IS NULL OR TipoTransacao = @Tipo)
      AND (@ApostaId     IS NULL OR ApostaId      = @ApostaId)
      AND (@DataInicio   IS NULL OR DataHora      >= @DataInicio)
      AND (@DataFim      IS NULL OR DataHora      <= @DataFim)
    ORDER BY DataHora DESC
    OFFSET @Offset ROWS FETCH NEXT 50 ROWS ONLY;
END
GO

-- ================================================================
-- UTILIZADOR E PERMISSOES
-- ================================================================
IF SUSER_ID('pagamentos') IS NULL
BEGIN
    PRINT '!!! LOGIN [pagamentos] NAO EXISTE. Rode apostas_credentials.example.sql primeiro. !!!';
END
ELSE
BEGIN
    IF USER_ID('pagamentos') IS NULL
    BEGIN
        CREATE USER [pagamentos] FOR LOGIN [pagamentos];
        PRINT 'utilizador pagamentos criado em rr1x24-pagamentos.';
    END
    ELSE
        PRINT 'utilizador pagamentos ja existe.';

    GRANT SELECT, INSERT, UPDATE ON dbo.Saldo_Utilizador TO [pagamentos];
    GRANT SELECT, INSERT        ON dbo.Transacoes        TO [pagamentos];
    GRANT EXECUTE ON dbo.sp_CriarSaldoUtilizador  TO [pagamentos];
    GRANT EXECUTE ON dbo.sp_ConsultarSaldo         TO [pagamentos];
    GRANT EXECUTE ON dbo.sp_ConsultarTodosSaldos   TO [pagamentos];
    GRANT EXECUTE ON dbo.sp_RegistarTransacao      TO [pagamentos];
    GRANT EXECUTE ON dbo.sp_ConsultarTransacoes    TO [pagamentos];
    PRINT 'permissoes atribuidas ao utilizador pagamentos.';
END
GO
