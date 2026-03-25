-- RODAR credentials.sql ANTES DESTE FICHEIRO
-- VER credentials.example.sql

USE [master];
GO

-- ============================================================
-- 1. CRIAR BASE DE DADOS
-- ============================================================
IF DB_ID(N'betstrike') IS NULL
BEGIN
    CREATE DATABASE [betstrike];
    PRINT 'database betstrike criada com sucesso.';
END
ELSE
BEGIN
    PRINT 'database betstrike ja existe.';
END
GO


-- ============================================================
-- 3. CRIAR TABELAS
-- ============================================================
USE [betstrike];
GO

BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID(N'dbo.jogo', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.jogo (
            id                  INT IDENTITY(1,1) PRIMARY KEY,
            codigo_jogo         VARCHAR(20)  NOT NULL UNIQUE,
            data_hora_inicio    DATETIME2    NOT NULL,
            equipa_casa         VARCHAR(120) NOT NULL,
            equipa_fora         VARCHAR(120) NOT NULL,
            tipo_competicao     VARCHAR(80)  NOT NULL,
            estado_atual        SMALLINT     NOT NULL,
            CONSTRAINT ck_jogo_codigo_formato
                CHECK (codigo_jogo LIKE 'FUT-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'),
            CONSTRAINT ck_jogo_estado_intervalo
                CHECK (estado_atual BETWEEN 1 AND 5)
        );
        PRINT 'tabela dbo.jogo criada.';
    END
    ELSE
        PRINT 'tabela dbo.jogo ja existe.';

    IF OBJECT_ID(N'dbo.resultado', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.resultado (
            id                      INT IDENTITY(1,1) PRIMARY KEY,
            jogo_id                 INT       NOT NULL UNIQUE,
            golos_casa              INT       NOT NULL,
            golos_visitante         INT       NOT NULL,
            data_hora_atualizacao   DATETIME2 NOT NULL,
            CONSTRAINT fk_resultado_jogo
                FOREIGN KEY (jogo_id)
                REFERENCES dbo.jogo (id)
                ON DELETE CASCADE,
            CONSTRAINT ck_resultado_golos_casa
                CHECK (golos_casa >= 0),
            CONSTRAINT ck_resultado_golos_visitante
                CHECK (golos_visitante >= 0)
        );
        PRINT 'tabela dbo.resultado criada.';
    END
    ELSE
        PRINT 'tabela dbo.resultado ja existe.';

    COMMIT TRAN;
    PRINT 'schema validado com sucesso.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0
        ROLLBACK TRAN;

    PRINT 'erro: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ============================================================
-- 4. CRIAR UTILIZADOR NA BASE DE DADOS E ATRIBUIR PERMISSOES
-- ============================================================
IF SUSER_ID('resultados') IS NULL
BEGIN
    PRINT '!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!';
    PRINT '!!!   LOGIN [resultados] NAO EXISTE            !!!';
    PRINT '!!!   Rode credentials.sql antes deste script  !!!';
    PRINT '!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!!';
END
ELSE
BEGIN
    IF USER_ID('resultados') IS NULL
    BEGIN
        CREATE USER [resultados] FOR LOGIN [resultados];
        PRINT 'utilizador resultados criado em betstrike.';
    END
    ELSE
        PRINT 'utilizador resultados ja existe em betstrike.';

    GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.jogo      TO [resultados];
    GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.resultado TO [resultados];
    PRINT 'permissoes atribuidas ao utilizador resultados.';
END
GO