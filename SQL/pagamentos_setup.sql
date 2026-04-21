-- Rodar apostas_credentials.example.sql antes deste ficheiro
-- Este script cria a base de dados betstrike-pagamentos e as suas tabelas

USE [master];
GO

IF DB_ID(N'betstrike-pagamentos') IS NULL
BEGIN
    CREATE DATABASE [betstrike-pagamentos];
    PRINT 'database betstrike-pagamentos criada.';
END
ELSE
    PRINT 'database betstrike-pagamentos ja existe.';
GO

USE [betstrike-pagamentos];
GO

BEGIN TRY
    BEGIN TRAN;

    -- ================================================================
    -- TABELA: Saldo_Utilizador
    -- ================================================================
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
    ELSE
        PRINT 'tabela Saldo_Utilizador ja existe.';

    -- ================================================================
    -- TABELA: Transacoes
    -- ================================================================
    IF OBJECT_ID(N'dbo.Transacoes', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Transacoes (
            Id               INT IDENTITY(1,1) PRIMARY KEY,
            ApostaId         INT           NULL,
            UtilizadorId     NVARCHAR(450) NOT NULL,
            TipoTransacao    CHAR(2)       NOT NULL,
            Valor            DECIMAL(10,2) NOT NULL,
            DataHora         DATETIME2     NOT NULL CONSTRAINT df_transacao_data DEFAULT (GETUTCDATE()),
            Estado           NVARCHAR(20)  NOT NULL CONSTRAINT df_transacao_estado DEFAULT ('Processada'),
            CONSTRAINT CK_Transacoes_Tipo   CHECK (TipoTransacao IN ('AP','PG','RE','DE','LV')),
            CONSTRAINT CK_Transacoes_Estado CHECK (Estado IN ('Pendente','Processada','Falhada','Reembolsada'))
        );
        PRINT 'tabela Transacoes criada.';
    END
    ELSE
        PRINT 'tabela Transacoes ja existe.';

    COMMIT TRAN;
    PRINT 'schema betstrike-pagamentos validado.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'erro: ' + ERROR_MESSAGE();
END CATCH;
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
        PRINT 'utilizador pagamentos criado em betstrike-pagamentos.';
    END
    ELSE
        PRINT 'utilizador pagamentos ja existe.';

    GRANT SELECT, INSERT, UPDATE ON dbo.Saldo_Utilizador TO [pagamentos];
    GRANT SELECT, INSERT, UPDATE ON dbo.Transacoes       TO [pagamentos];
    PRINT 'permissoes atribuidas ao utilizador pagamentos.';
END
GO
