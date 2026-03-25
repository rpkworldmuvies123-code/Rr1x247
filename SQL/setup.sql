-- RODAR credentials.sql ANTES DESTE FICHEIRO
-- VER credentials.example.sql

USE [master];
GO

-- ============================================================
-- 1. CRIAR BASE DE DADOS
-- ============================================================
IF DB_ID(N'betstrike-resultados') IS NULL
BEGIN
    CREATE DATABASE [betstrike-resultados];
    PRINT 'database betstrike-resultados criada com sucesso.';
END
ELSE
BEGIN
    PRINT 'database betstrike-resultados ja existe.';
END
GO


-- ============================================================
-- 3. CRIAR TABELAS
-- ============================================================
USE [betstrike-resultados];
GO

BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID(N'dbo.jogo', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.jogo (
            id                    INT IDENTITY(1,1) PRIMARY KEY,
            codigo_jogo           VARCHAR(20)  NOT NULL UNIQUE,
            data_hora_inicio      DATETIME2    NOT NULL,
            equipa_casa           VARCHAR(120) NOT NULL,
            equipa_fora           VARCHAR(120) NOT NULL,
            tipo_competicao       VARCHAR(80)  NOT NULL,
            estado_atual          SMALLINT     NOT NULL,
            golos_casa_atual      INT          NOT NULL CONSTRAINT df_jogo_golos_casa_atual DEFAULT(0),
            golos_visitante_atual INT          NOT NULL CONSTRAINT df_jogo_golos_visitante_atual DEFAULT(0),
            marcador_atualizado   BIT          NOT NULL CONSTRAINT df_jogo_marcador_atualizado DEFAULT(0),
            CONSTRAINT ck_jogo_codigo_formato
                CHECK (codigo_jogo LIKE 'FUT-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'),
            CONSTRAINT ck_jogo_estado_intervalo
                CHECK (estado_atual BETWEEN 1 AND 5),
            CONSTRAINT ck_jogo_golos_casa_atual
                CHECK (golos_casa_atual >= 0),
            CONSTRAINT ck_jogo_golos_visitante_atual
                CHECK (golos_visitante_atual >= 0)
        );
        PRINT 'tabela dbo.jogo criada.';
    END
    ELSE
    BEGIN
        PRINT 'tabela dbo.jogo ja existe.';

        IF COL_LENGTH('dbo.jogo', 'golos_casa_atual') IS NULL
        BEGIN
            ALTER TABLE dbo.jogo
                ADD golos_casa_atual INT NOT NULL CONSTRAINT df_jogo_golos_casa_atual DEFAULT(0);
            PRINT 'coluna dbo.jogo.golos_casa_atual adicionada.';
        END

        IF COL_LENGTH('dbo.jogo', 'golos_visitante_atual') IS NULL
        BEGIN
            ALTER TABLE dbo.jogo
                ADD golos_visitante_atual INT NOT NULL CONSTRAINT df_jogo_golos_visitante_atual DEFAULT(0);
            PRINT 'coluna dbo.jogo.golos_visitante_atual adicionada.';
        END

        IF COL_LENGTH('dbo.jogo', 'marcador_atualizado') IS NULL
        BEGIN
            ALTER TABLE dbo.jogo
                ADD marcador_atualizado BIT NOT NULL CONSTRAINT df_jogo_marcador_atualizado DEFAULT(0);
            PRINT 'coluna dbo.jogo.marcador_atualizado adicionada.';
        END

        IF OBJECT_ID(N'dbo.ck_jogo_golos_casa_atual', N'C') IS NULL
        BEGIN
            ALTER TABLE dbo.jogo
                ADD CONSTRAINT ck_jogo_golos_casa_atual CHECK (golos_casa_atual >= 0);
            PRINT 'constraint ck_jogo_golos_casa_atual adicionada.';
        END

        IF OBJECT_ID(N'dbo.ck_jogo_golos_visitante_atual', N'C') IS NULL
        BEGIN
            ALTER TABLE dbo.jogo
                ADD CONSTRAINT ck_jogo_golos_visitante_atual CHECK (golos_visitante_atual >= 0);
            PRINT 'constraint ck_jogo_golos_visitante_atual adicionada.';
        END
    END

    IF OBJECT_ID(N'dbo.resultado', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.resultado (
            id                      INT IDENTITY(1,1) PRIMARY KEY,
            jogo_id                 INT       NOT NULL UNIQUE,
            golos_casa              INT       NOT NULL,
            golos_visitante         INT       NOT NULL,
            resultado               CHAR(1)   NOT NULL CONSTRAINT df_resultado_resultado DEFAULT('0'),
            data_hora_atualizacao   DATETIME2 NOT NULL,
            CONSTRAINT fk_resultado_jogo
                FOREIGN KEY (jogo_id)
                REFERENCES dbo.jogo (id)
                ON DELETE CASCADE,
            CONSTRAINT ck_resultado_golos_casa
                CHECK (golos_casa >= 0),
            CONSTRAINT ck_resultado_golos_visitante
                CHECK (golos_visitante >= 0),
            CONSTRAINT ck_resultado_tipo
                CHECK (resultado IN ('0', '1', '2', 'X'))
        );
        PRINT 'tabela dbo.resultado criada.';
    END
    ELSE
    BEGIN
        PRINT 'tabela dbo.resultado ja existe.';

        IF COL_LENGTH('dbo.resultado', 'resultado') IS NULL
        BEGIN
            ALTER TABLE dbo.resultado
                ADD resultado CHAR(1) NOT NULL
                    CONSTRAINT df_resultado_resultado DEFAULT('0');
            PRINT 'coluna dbo.resultado.resultado adicionada.';
        END

        IF OBJECT_ID(N'dbo.ck_resultado_tipo', N'C') IS NULL
        BEGIN
            ALTER TABLE dbo.resultado
                ADD CONSTRAINT ck_resultado_tipo
                    CHECK (resultado IN ('0', '1', '2', 'X'));
            PRINT 'constraint ck_resultado_tipo adicionada.';
        END
    END

    IF OBJECT_ID(N'dbo.trg_resultado_apenas_finalizado', N'TR') IS NULL
    BEGIN
        EXEC('CREATE TRIGGER dbo.trg_resultado_apenas_finalizado
              ON dbo.resultado
              AFTER INSERT, UPDATE
              AS
              BEGIN
                  SET NOCOUNT ON;

                  IF EXISTS (
                      SELECT 1
                      FROM inserted i
                      INNER JOIN dbo.jogo j ON j.id = i.jogo_id
                      WHERE j.estado_atual <> 3
                  )
                  BEGIN
                      THROW 50001, ''resultado so pode ser inserido/atualizado para jogos finalizados (estado 3).'', 1;
                  END
              END');
        PRINT 'trigger dbo.trg_resultado_apenas_finalizado criada.';
    END
    ELSE
        PRINT 'trigger dbo.trg_resultado_apenas_finalizado ja existe.';

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
        PRINT 'utilizador resultados criado em betstrike-resultados.';
    END
    ELSE
        PRINT 'utilizador resultados ja existe em betstrike-resultados.';

    GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.jogo      TO [resultados];
    GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.resultado TO [resultados];
    PRINT 'permissoes atribuidas ao utilizador resultados.';
END
GO