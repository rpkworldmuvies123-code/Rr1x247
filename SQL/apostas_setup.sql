-- Pré-requisitos: apostas_credentials.example.sql + pagamentos_setup.sql

USE [master];
GO

IF DB_ID(N'rr1x24-apostas') IS NULL
BEGIN
    CREATE DATABASE [rr1x24-apostas];
    PRINT 'database rr1x24-apostas criada.';
END
ELSE
    PRINT 'database rr1x24-apostas ja existe.';
GO

USE [rr1x24-apostas];
GO

-- ================================================================
-- TABELAS
-- ================================================================
BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID(N'dbo.Jogos', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Jogos (
            Id              INT IDENTITY(1,1) PRIMARY KEY,
            CodigoJogo      VARCHAR(20)   NOT NULL,
            DataHoraInicio  DATETIME2     NOT NULL,
            EquipaCasa      VARCHAR(120)  NOT NULL,
            EquipaFora      VARCHAR(120)  NOT NULL,
            TipoCompeticao  VARCHAR(80)   NOT NULL,
            Estado          SMALLINT      NOT NULL CONSTRAINT df_jogos_estado DEFAULT (1),
            GolosCasa       INT           NOT NULL CONSTRAINT df_jogos_gc     DEFAULT (0),
            GolosVisitante  INT           NOT NULL CONSTRAINT df_jogos_gv     DEFAULT (0),
            Odd1            DECIMAL(10,4) NOT NULL CONSTRAINT df_jogos_odd1   DEFAULT (2.10),
            OddX            DECIMAL(10,4) NOT NULL CONSTRAINT df_jogos_oddx   DEFAULT (3.50),
            Odd2            DECIMAL(10,4) NOT NULL CONSTRAINT df_jogos_odd2   DEFAULT (3.20),
            CONSTRAINT UQ_Jogos_CodigoJogo   UNIQUE  (CodigoJogo),
            CONSTRAINT CK_Jogos_CodigoFormato CHECK  (CodigoJogo LIKE 'FUT-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'),
            CONSTRAINT CK_Jogos_Estado        CHECK  (Estado BETWEEN 1 AND 5),
            CONSTRAINT CK_Jogos_GolosCasa     CHECK  (GolosCasa >= 0),
            CONSTRAINT CK_Jogos_GolosVisitante CHECK (GolosVisitante >= 0),
            CONSTRAINT CK_Jogos_Odd1          CHECK  (Odd1 > 1.0),
            CONSTRAINT CK_Jogos_OddX          CHECK  (OddX > 1.0),
            CONSTRAINT CK_Jogos_Odd2          CHECK  (Odd2 > 1.0)
        );
        PRINT 'tabela Jogos criada.';
    END
    ELSE
    BEGIN
        PRINT 'tabela Jogos ja existe.';
        IF COL_LENGTH('dbo.Jogos', 'Odd1') IS NULL
        BEGIN
            ALTER TABLE dbo.Jogos ADD
                Odd1 DECIMAL(10,4) NOT NULL CONSTRAINT df_jogos_odd1 DEFAULT (2.10),
                OddX DECIMAL(10,4) NOT NULL CONSTRAINT df_jogos_oddx DEFAULT (3.50),
                Odd2 DECIMAL(10,4) NOT NULL CONSTRAINT df_jogos_odd2 DEFAULT (3.20);
            PRINT 'colunas Odd1/OddX/Odd2 adicionadas a tabela Jogos.';
        END
    END

    IF OBJECT_ID(N'dbo.Resultados', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Resultados (
            Id                   INT IDENTITY(1,1) PRIMARY KEY,
            JogoId               INT           NOT NULL,
            GolosCasa            INT           NOT NULL CONSTRAINT df_res_gc DEFAULT (0),
            GolosVisitante       INT           NOT NULL CONSTRAINT df_res_gv DEFAULT (0),
            DataHoraAtualizacao  DATETIME2     NOT NULL CONSTRAINT df_res_data DEFAULT (GETUTCDATE()),
            CONSTRAINT UQ_Resultados_JogoId    UNIQUE  (JogoId),
            CONSTRAINT FK_Resultados_Jogos     FOREIGN KEY (JogoId) REFERENCES dbo.Jogos(Id) ON DELETE CASCADE,
            CONSTRAINT CK_Resultados_GolosCasa     CHECK (GolosCasa >= 0),
            CONSTRAINT CK_Resultados_GolosVisitante CHECK (GolosVisitante >= 0)
        );
        PRINT 'tabela Resultados criada.';
    END
    ELSE PRINT 'tabela Resultados ja existe.';

    IF OBJECT_ID(N'dbo.Apostas', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.Apostas (
            Id                  INT IDENTITY(1,1) PRIMARY KEY,
            JogoId              INT           NOT NULL,
            UtilizadorId        NVARCHAR(450) NOT NULL,
            TipoAposta          CHAR(1)       NOT NULL,
            MontanteApostado    DECIMAL(10,2) NOT NULL,
            OddMomento          DECIMAL(10,4) NOT NULL,
            Estado              SMALLINT      NOT NULL CONSTRAINT df_apostas_estado  DEFAULT (1),
            DataHoraRegisto     DATETIME2     NOT NULL CONSTRAINT df_apostas_data    DEFAULT (GETUTCDATE()),
            PagamentoPendente   BIT           NOT NULL CONSTRAINT df_apostas_pgpend  DEFAULT (0),
            CONSTRAINT FK_Apostas_Jogos       FOREIGN KEY (JogoId) REFERENCES dbo.Jogos(Id),
            CONSTRAINT CK_Apostas_TipoAposta  CHECK (TipoAposta IN ('1','X','2')),
            CONSTRAINT CK_Apostas_Estado      CHECK (Estado BETWEEN 1 AND 4),
            CONSTRAINT CK_Apostas_Montante    CHECK (MontanteApostado > 0),
            CONSTRAINT CK_Apostas_Odd         CHECK (OddMomento > 1.0)
        );
        PRINT 'tabela Apostas criada.';
    END
    ELSE
    BEGIN
        PRINT 'tabela Apostas ja existe.';
        IF COL_LENGTH('dbo.Apostas', 'PagamentoPendente') IS NULL
        BEGIN
            ALTER TABLE dbo.Apostas ADD
                PagamentoPendente BIT NOT NULL CONSTRAINT df_apostas_pgpend DEFAULT (0);
            PRINT 'coluna PagamentoPendente adicionada a tabela Apostas.';
        END
    END

    COMMIT TRAN;
    PRINT 'tabelas validadas.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'erro tabelas: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ================================================================
-- TABELAS IDENTITY (ASP.NET Core Identity)
-- ================================================================
BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID(N'dbo.AspNetRoles', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AspNetRoles (
            Id               NVARCHAR(450) NOT NULL,
            Name             NVARCHAR(256) NULL,
            NormalizedName   NVARCHAR(256) NULL,
            ConcurrencyStamp NVARCHAR(MAX) NULL,
            CONSTRAINT PK_AspNetRoles PRIMARY KEY (Id)
        );
        CREATE UNIQUE INDEX RoleNameIndex ON dbo.AspNetRoles (NormalizedName)
            WHERE NormalizedName IS NOT NULL;
        PRINT 'tabela AspNetRoles criada.';
    END
    ELSE PRINT 'tabela AspNetRoles ja existe.';

    IF OBJECT_ID(N'dbo.AspNetUsers', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AspNetUsers (
            Id                   NVARCHAR(450)  NOT NULL,
            NomeCompleto         NVARCHAR(MAX)  NOT NULL CONSTRAINT df_users_nome DEFAULT (''),
            DataRegisto          DATETIME2      NOT NULL CONSTRAINT df_users_data DEFAULT (GETUTCDATE()),
            UserName             NVARCHAR(256)  NULL,
            NormalizedUserName   NVARCHAR(256)  NULL,
            Email                NVARCHAR(256)  NULL,
            NormalizedEmail      NVARCHAR(256)  NULL,
            EmailConfirmed       BIT            NOT NULL,
            PasswordHash         NVARCHAR(MAX)  NULL,
            SecurityStamp        NVARCHAR(MAX)  NULL,
            ConcurrencyStamp     NVARCHAR(MAX)  NULL,
            PhoneNumber          NVARCHAR(MAX)  NULL,
            PhoneNumberConfirmed BIT            NOT NULL,
            TwoFactorEnabled     BIT            NOT NULL,
            LockoutEnd           DATETIMEOFFSET NULL,
            LockoutEnabled       BIT            NOT NULL,
            AccessFailedCount    INT            NOT NULL,
            CONSTRAINT PK_AspNetUsers PRIMARY KEY (Id)
        );
        CREATE INDEX EmailIndex ON dbo.AspNetUsers (NormalizedEmail);
        CREATE UNIQUE INDEX UserNameIndex ON dbo.AspNetUsers (NormalizedUserName)
            WHERE NormalizedUserName IS NOT NULL;
        PRINT 'tabela AspNetUsers criada.';
    END
    ELSE PRINT 'tabela AspNetUsers ja existe.';

    IF OBJECT_ID(N'dbo.AspNetRoleClaims', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AspNetRoleClaims (
            Id         INT IDENTITY NOT NULL,
            RoleId     NVARCHAR(450) NOT NULL,
            ClaimType  NVARCHAR(MAX) NULL,
            ClaimValue NVARCHAR(MAX) NULL,
            CONSTRAINT PK_AspNetRoleClaims PRIMARY KEY (Id),
            CONSTRAINT FK_AspNetRoleClaims_Roles FOREIGN KEY (RoleId)
                REFERENCES dbo.AspNetRoles(Id) ON DELETE CASCADE
        );
        CREATE INDEX IX_AspNetRoleClaims_RoleId ON dbo.AspNetRoleClaims (RoleId);
        PRINT 'tabela AspNetRoleClaims criada.';
    END
    ELSE PRINT 'tabela AspNetRoleClaims ja existe.';

    IF OBJECT_ID(N'dbo.AspNetUserClaims', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AspNetUserClaims (
            Id         INT IDENTITY NOT NULL,
            UserId     NVARCHAR(450) NOT NULL,
            ClaimType  NVARCHAR(MAX) NULL,
            ClaimValue NVARCHAR(MAX) NULL,
            CONSTRAINT PK_AspNetUserClaims PRIMARY KEY (Id),
            CONSTRAINT FK_AspNetUserClaims_Users FOREIGN KEY (UserId)
                REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
        );
        CREATE INDEX IX_AspNetUserClaims_UserId ON dbo.AspNetUserClaims (UserId);
        PRINT 'tabela AspNetUserClaims criada.';
    END
    ELSE PRINT 'tabela AspNetUserClaims ja existe.';

    IF OBJECT_ID(N'dbo.AspNetUserLogins', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AspNetUserLogins (
            LoginProvider       NVARCHAR(128) NOT NULL,
            ProviderKey         NVARCHAR(128) NOT NULL,
            ProviderDisplayName NVARCHAR(MAX) NULL,
            UserId              NVARCHAR(450) NOT NULL,
            CONSTRAINT PK_AspNetUserLogins PRIMARY KEY (LoginProvider, ProviderKey),
            CONSTRAINT FK_AspNetUserLogins_Users FOREIGN KEY (UserId)
                REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
        );
        CREATE INDEX IX_AspNetUserLogins_UserId ON dbo.AspNetUserLogins (UserId);
        PRINT 'tabela AspNetUserLogins criada.';
    END
    ELSE PRINT 'tabela AspNetUserLogins ja existe.';

    IF OBJECT_ID(N'dbo.AspNetUserRoles', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AspNetUserRoles (
            UserId NVARCHAR(450) NOT NULL,
            RoleId NVARCHAR(450) NOT NULL,
            CONSTRAINT PK_AspNetUserRoles PRIMARY KEY (UserId, RoleId),
            CONSTRAINT FK_AspNetUserRoles_Roles FOREIGN KEY (RoleId)
                REFERENCES dbo.AspNetRoles(Id) ON DELETE CASCADE,
            CONSTRAINT FK_AspNetUserRoles_Users FOREIGN KEY (UserId)
                REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
        );
        CREATE INDEX IX_AspNetUserRoles_RoleId ON dbo.AspNetUserRoles (RoleId);
        PRINT 'tabela AspNetUserRoles criada.';
    END
    ELSE PRINT 'tabela AspNetUserRoles ja existe.';

    IF OBJECT_ID(N'dbo.AspNetUserTokens', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.AspNetUserTokens (
            UserId        NVARCHAR(450) NOT NULL,
            LoginProvider NVARCHAR(128) NOT NULL,
            Name          NVARCHAR(128) NOT NULL,
            Value         NVARCHAR(MAX) NULL,
            CONSTRAINT PK_AspNetUserTokens PRIMARY KEY (UserId, LoginProvider, Name),
            CONSTRAINT FK_AspNetUserTokens_Users FOREIGN KEY (UserId)
                REFERENCES dbo.AspNetUsers(Id) ON DELETE CASCADE
        );
        PRINT 'tabela AspNetUserTokens criada.';
    END
    ELSE PRINT 'tabela AspNetUserTokens ja existe.';

    COMMIT TRAN;
    PRINT 'tabelas Identity validadas.';
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'erro tabelas Identity: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ================================================================
-- STORED PROCEDURES
-- ================================================================

-- ----------------------------------------------------------------
-- sp_InserirJogo
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_InserirJogo
    @CodigoJogo     VARCHAR(20),
    @DataHoraInicio DATETIME2,
    @EquipaCasa     VARCHAR(120),
    @EquipaFora     VARCHAR(120),
    @TipoCompeticao VARCHAR(80),
    @Id             INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;

    IF @CodigoJogo NOT LIKE 'FUT-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'
        THROW 50001, 'Formato de codigo de jogo invalido (FUT-AAAA-JJNN).', 1;

    IF EXISTS (SELECT 1 FROM dbo.Jogos WHERE CodigoJogo = @CodigoJogo)
        THROW 50002, 'Ja existe um jogo com este codigo.', 1;

    INSERT INTO dbo.Jogos (CodigoJogo, DataHoraInicio, EquipaCasa, EquipaFora, TipoCompeticao, Estado)
    VALUES (@CodigoJogo, @DataHoraInicio, @EquipaCasa, @EquipaFora, @TipoCompeticao, 1);

    SET @Id = SCOPE_IDENTITY();
END
GO

-- ----------------------------------------------------------------
-- sp_AtualizarJogo
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_AtualizarJogo
    @CodigoJogo      VARCHAR(20),
    @Estado          SMALLINT  = NULL,
    @GolosCasa       INT       = NULL,
    @GolosVisitante  INT       = NULL
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @JogoId INT, @EstadoAtual SMALLINT;
    SELECT @JogoId = Id, @EstadoAtual = Estado FROM dbo.Jogos WHERE CodigoJogo = @CodigoJogo;

    IF @JogoId IS NULL
        THROW 50003, 'Jogo nao encontrado.', 1;

    IF @Estado IS NOT NULL
    BEGIN
        IF @EstadoAtual IN (3, 4, 5)
            THROW 50004, 'Nao e possivel alterar o estado de um jogo em estado terminal.', 1;

        -- Validate valid transitions
        IF @EstadoAtual = 2 AND @Estado = 1
            THROW 50005, 'Transicao de estado invalida: nao e possivel voltar a Agendado.', 1;

        UPDATE dbo.Jogos SET Estado = @Estado WHERE Id = @JogoId;
    END

    IF @GolosCasa IS NOT NULL AND @GolosVisitante IS NOT NULL
    BEGIN
        UPDATE dbo.Jogos
        SET GolosCasa = @GolosCasa, GolosVisitante = @GolosVisitante
        WHERE Id = @JogoId;
    END

    -- Transitioning to Finalizado (3)
    IF @Estado = 3
    BEGIN
        -- Auto-insert result with current score if none exists
        IF NOT EXISTS (SELECT 1 FROM dbo.Resultados WHERE JogoId = @JogoId)
        BEGIN
            DECLARE @GC INT, @GV INT;
            SELECT @GC = GolosCasa, @GV = GolosVisitante FROM dbo.Jogos WHERE Id = @JogoId;
            INSERT INTO dbo.Resultados (JogoId, GolosCasa, GolosVisitante)
            VALUES (@JogoId, @GC, @GV);
        END
        EXEC dbo.sp_ResolverApostasJogo @JogoId;
    END

    -- Transitioning to Cancelado (4) or Adiado (5): annul all pending bets + marcar para reembolso
    IF @Estado IN (4, 5)
    BEGIN
        UPDATE dbo.Apostas
        SET Estado = 4, PagamentoPendente = 1
        WHERE JogoId = @JogoId AND Estado = 1;
    END

    COMMIT TRANSACTION;
END
GO

-- ----------------------------------------------------------------
-- sp_RemoverJogo
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_RemoverJogo
    @CodigoJogo VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;

    DECLARE @JogoId INT, @Estado SMALLINT;
    SELECT @JogoId = Id, @Estado = Estado FROM dbo.Jogos WHERE CodigoJogo = @CodigoJogo;

    IF @JogoId IS NULL
        THROW 50003, 'Jogo nao encontrado.', 1;
    IF @Estado != 1
        THROW 50020, 'So e permitido remover jogos no estado Agendado.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Apostas WHERE JogoId = @JogoId)
        THROW 50019, 'Nao e possivel remover jogos com apostas associadas.', 1;

    DELETE FROM dbo.Jogos WHERE Id = @JogoId;
END
GO

-- ----------------------------------------------------------------
-- sp_ConsultarJogos
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarJogos
    @Data           DATE       = NULL,
    @Estado         SMALLINT   = NULL,
    @TipoCompeticao VARCHAR(80) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT Id, CodigoJogo, DataHoraInicio, EquipaCasa, EquipaFora, TipoCompeticao,
           Estado, GolosCasa, GolosVisitante
    FROM dbo.Jogos
    WHERE (@Data           IS NULL OR CAST(DataHoraInicio AS DATE) = @Data)
      AND (@Estado         IS NULL OR Estado         = @Estado)
      AND (@TipoCompeticao IS NULL OR TipoCompeticao = @TipoCompeticao)
    ORDER BY DataHoraInicio;
END
GO

-- ----------------------------------------------------------------
-- sp_ConsultarJogo
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarJogo
    @CodigoJogo VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        j.Id, j.CodigoJogo, j.DataHoraInicio, j.EquipaCasa, j.EquipaFora,
        j.TipoCompeticao, j.Estado, j.GolosCasa, j.GolosVisitante,
        j.Odd1, j.OddX, j.Odd2,
        ISNULL((SELECT COUNT(*) FROM dbo.Apostas a WHERE a.JogoId = j.Id AND a.Estado = 1), 0) AS ApostasPendentes,
        ISNULL((SELECT SUM(a2.MontanteApostado) FROM dbo.Apostas a2 WHERE a2.JogoId = j.Id AND a2.Estado = 1), 0) AS VolumeTotalApostado
    FROM dbo.Jogos j
    WHERE j.CodigoJogo = @CodigoJogo;
END
GO

-- ----------------------------------------------------------------
-- sp_ResolverApostasJogo
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ResolverApostasJogo
    @JogoId INT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;

    DECLARE @GC INT, @GV INT;
    SELECT @GC = GolosCasa, @GV = GolosVisitante FROM dbo.Resultados WHERE JogoId = @JogoId;

    IF @GC IS NULL
        THROW 50006, 'Resultado nao encontrado para resolver apostas.', 1;

    UPDATE dbo.Apostas
    SET Estado =
            CASE
                WHEN TipoAposta = '1' AND @GC  > @GV THEN 2
                WHEN TipoAposta = 'X' AND @GC  = @GV THEN 2
                WHEN TipoAposta = '2' AND @GV  > @GC THEN 2
                ELSE 3
            END,
        -- ganhadoras precisam de pagamento; perdidas nao (debito ja ocorreu)
        PagamentoPendente =
            CASE
                WHEN TipoAposta = '1' AND @GC  > @GV THEN 1
                WHEN TipoAposta = 'X' AND @GC  = @GV THEN 1
                WHEN TipoAposta = '2' AND @GV  > @GC THEN 1
                ELSE 0
            END
    WHERE JogoId = @JogoId AND Estado = 1;
END
GO

-- ----------------------------------------------------------------
-- sp_InserirAposta
--   Valida e insere a aposta. Nao faz operacoes financeiras —
--   o debito e feito pela ApiApostas via ApiPagamentos (HTTP).
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_InserirAposta
    @CodigoJogo       VARCHAR(20),
    @UtilizadorId     NVARCHAR(450),
    @TipoAposta       CHAR(1),
    @MontanteApostado DECIMAL(10,2),
    @ApostaId         INT          OUTPUT,
    @OddMomento       DECIMAL(10,4) OUTPUT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;

    DECLARE @JogoId INT, @Estado SMALLINT, @Odd1 DECIMAL(10,4), @OddX DECIMAL(10,4), @Odd2 DECIMAL(10,4);
    SELECT @JogoId = Id, @Estado = Estado, @Odd1 = Odd1, @OddX = OddX, @Odd2 = Odd2
    FROM dbo.Jogos WHERE CodigoJogo = @CodigoJogo;

    IF @JogoId IS NULL           THROW 50007, 'Jogo nao encontrado.', 1;
    IF @Estado NOT IN (1,2)      THROW 50008, 'Nao e permitido registar apostas em jogos com este estado.', 1;
    IF @TipoAposta NOT IN ('1','X','2') THROW 50009, 'O tipo de aposta deve ser 1, X ou 2.', 1;
    IF @MontanteApostado <= 0    THROW 50010, 'O montante apostado deve ser positivo.', 1;

    SET @OddMomento = CASE @TipoAposta WHEN '1' THEN @Odd1 WHEN 'X' THEN @OddX ELSE @Odd2 END;

    IF @OddMomento IS NULL OR @OddMomento <= 1.0
        THROW 50011, 'A odd configurada para este mercado e invalida.', 1;

    INSERT INTO dbo.Apostas (JogoId, UtilizadorId, TipoAposta, MontanteApostado, OddMomento, Estado, DataHoraRegisto)
    VALUES (@JogoId, @UtilizadorId, @TipoAposta, @MontanteApostado, @OddMomento, 1, GETUTCDATE());
    SET @ApostaId = SCOPE_IDENTITY();
END
GO

-- ----------------------------------------------------------------
-- sp_RemoverAposta  — compensacao: elimina aposta nao activada financeiramente
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_RemoverAposta
    @ApostaId INT
AS
BEGIN
    SET NOCOUNT ON;
    DELETE FROM dbo.Apostas WHERE Id = @ApostaId AND Estado = 1;
END
GO

-- ----------------------------------------------------------------
-- sp_CancelarAposta
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_CancelarAposta
    @ApostaId     INT,
    @UtilizadorId NVARCHAR(450),
    @IsAdmin      BIT = 0
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;

    DECLARE @JogoId INT, @EstadoAposta SMALLINT, @EstadoJogo SMALLINT, @DonoDaAposta NVARCHAR(450);
    SELECT @JogoId = a.JogoId, @EstadoAposta = a.Estado, @DonoDaAposta = a.UtilizadorId,
           @EstadoJogo = j.Estado
    FROM dbo.Apostas a
    JOIN dbo.Jogos j ON j.Id = a.JogoId
    WHERE a.Id = @ApostaId;

    IF @JogoId IS NULL      THROW 50014, N'Aposta nao encontrada.', 1;
    IF @IsAdmin = 0 AND @DonoDaAposta != @UtilizadorId
                            THROW 50016, N'Nao tem permissao para cancelar esta aposta.', 1;
    IF @EstadoAposta != 1   THROW 50015, N'Apenas apostas pendentes podem ser canceladas.', 1;
    IF @EstadoJogo != 1     THROW 50015, N'So e permitido cancelar apostas enquanto o jogo esta no estado Agendado.', 1;

    -- PagamentoPendente=1: ApiApostas processara o reembolso via ApiPagamentos
    UPDATE dbo.Apostas
    SET Estado = 4, PagamentoPendente = 1
    WHERE Id = @ApostaId;
END
GO

-- ----------------------------------------------------------------
-- sp_ConsultarApostas
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarApostas
    @UtilizadorId NVARCHAR(450) = NULL,
    @CodigoJogo   VARCHAR(20)   = NULL,
    @Estado       SMALLINT      = NULL,
    @DataInicio   DATETIME2     = NULL,
    @DataFim      DATETIME2     = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        a.Id, a.JogoId, j.CodigoJogo, j.EquipaCasa, j.EquipaFora,
        a.UtilizadorId, a.TipoAposta, a.MontanteApostado, a.OddMomento,
        a.Estado, a.DataHoraRegisto,
        CAST(a.MontanteApostado * a.OddMomento AS DECIMAL(10,2)) AS PremioPotencial
    FROM dbo.Apostas a
    JOIN dbo.Jogos j ON j.Id = a.JogoId
    WHERE (@UtilizadorId IS NULL OR a.UtilizadorId = @UtilizadorId)
      AND (@CodigoJogo   IS NULL OR j.CodigoJogo   = @CodigoJogo)
      AND (@Estado       IS NULL OR a.Estado        = @Estado)
      AND (@DataInicio   IS NULL OR a.DataHoraRegisto >= @DataInicio)
      AND (@DataFim      IS NULL OR a.DataHoraRegisto <= @DataFim)
    ORDER BY a.DataHoraRegisto DESC;
END
GO

-- ----------------------------------------------------------------
-- sp_ConsultarAposta
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarAposta
    @ApostaId     INT,
    @UtilizadorId NVARCHAR(450) = NULL,
    @IsAdmin      BIT           = 0
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        a.Id, a.JogoId, j.CodigoJogo, j.EquipaCasa, j.EquipaFora, j.TipoCompeticao,
        a.UtilizadorId, a.TipoAposta, a.MontanteApostado, a.OddMomento,
        a.Estado, a.DataHoraRegisto,
        CAST(a.MontanteApostado * a.OddMomento AS DECIMAL(10,2)) AS PremioPotencial
    FROM dbo.Apostas a
    JOIN dbo.Jogos j ON j.Id = a.JogoId
    WHERE a.Id = @ApostaId
      AND (@IsAdmin = 1 OR a.UtilizadorId = @UtilizadorId);
END
GO

-- ----------------------------------------------------------------
-- sp_InserirResultado
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_InserirResultado
    @CodigoJogo    VARCHAR(20),
    @GolosCasa     INT,
    @GolosVisitante INT,
    @Id            INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @JogoId INT, @Estado SMALLINT;
    SELECT @JogoId = Id, @Estado = Estado FROM dbo.Jogos WHERE CodigoJogo = @CodigoJogo;

    IF @JogoId IS NULL THROW 50003, 'Jogo nao encontrado.', 1;
    IF @Estado != 3    THROW 50018, 'Apenas jogos no estado Finalizado podem ter resultado.', 1;
    IF EXISTS (SELECT 1 FROM dbo.Resultados WHERE JogoId = @JogoId)
               THROW 50017, 'Ja existe um resultado para este jogo.', 1;

    INSERT INTO dbo.Resultados (JogoId, GolosCasa, GolosVisitante)
    VALUES (@JogoId, @GolosCasa, @GolosVisitante);
    SET @Id = SCOPE_IDENTITY();

    COMMIT TRANSACTION;
END
GO

-- ----------------------------------------------------------------
-- sp_ConsultarResultadoJogo
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarResultadoJogo
    @CodigoJogo VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT r.Id, r.JogoId, j.CodigoJogo, j.EquipaCasa, j.EquipaFora,
           r.GolosCasa, r.GolosVisitante, r.DataHoraAtualizacao
    FROM dbo.Resultados r
    JOIN dbo.Jogos j ON j.Id = r.JogoId
    WHERE j.CodigoJogo = @CodigoJogo;
END
GO

-- ----------------------------------------------------------------
-- sp_EstatisticasJogo
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_EstatisticasJogo
    @CodigoJogo VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @JogoId INT;
    SELECT @JogoId = Id FROM dbo.Jogos WHERE CodigoJogo = @CodigoJogo;
    IF @JogoId IS NULL THROW 50003, 'Jogo nao encontrado.', 1;

    SELECT
        j.CodigoJogo,
        ISNULL(SUM(a.MontanteApostado), 0)                                          AS TotalApostado,
        COUNT(a.Id)                                                                  AS TotalApostas,
        SUM(CASE WHEN a.TipoAposta = '1' THEN 1 ELSE 0 END)                         AS Apostas1,
        SUM(CASE WHEN a.TipoAposta = 'X' THEN 1 ELSE 0 END)                         AS ApostasX,
        SUM(CASE WHEN a.TipoAposta = '2' THEN 1 ELSE 0 END)                         AS Apostas2,
        SUM(CASE WHEN a.Estado = 1 THEN 1 ELSE 0 END)                               AS ApostasPendentes,
        SUM(CASE WHEN a.Estado = 2 THEN 1 ELSE 0 END)                               AS ApostasGanhas,
        SUM(CASE WHEN a.Estado = 3 THEN 1 ELSE 0 END)                               AS ApostasPerdidas,
        SUM(CASE WHEN a.Estado = 4 THEN 1 ELSE 0 END)                               AS ApostasAnuladas,
        ISNULL(SUM(CASE WHEN a.Estado = 3 THEN a.MontanteApostado ELSE 0 END), 0) -
        ISNULL(SUM(CASE WHEN a.Estado = 2 THEN a.MontanteApostado * a.OddMomento - a.MontanteApostado ELSE 0 END), 0)
                                                                                     AS MargemPlataforma
    FROM dbo.Jogos j
    LEFT JOIN dbo.Apostas a ON a.JogoId = j.Id
    WHERE j.Id = @JogoId
    GROUP BY j.CodigoJogo;
END
GO

-- ----------------------------------------------------------------
-- sp_EstatisticasCompeticao
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_EstatisticasCompeticao
    @TipoCompeticao VARCHAR(80)
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @TotalJogosFinalizados INT;
    SELECT @TotalJogosFinalizados = COUNT(*) FROM dbo.Jogos
    WHERE TipoCompeticao = @TipoCompeticao AND Estado = 3;

    SELECT
        @TipoCompeticao                                                              AS TipoCompeticao,
        COUNT(DISTINCT j.Id)                                                         AS TotalJogos,
        ISNULL(AVG(CAST(r.GolosCasa + r.GolosVisitante AS FLOAT)), 0)               AS MediaGolosPorJogo,
        ISNULL(SUM(a.MontanteApostado), 0)                                          AS VolumeTotalApostado,
        CASE WHEN @TotalJogosFinalizados = 0 THEN 0
             ELSE CAST(SUM(CASE WHEN r.GolosCasa > r.GolosVisitante THEN 1 ELSE 0 END) AS FLOAT) / @TotalJogosFinalizados
        END                                                                          AS TaxaVitoria1,
        CASE WHEN @TotalJogosFinalizados = 0 THEN 0
             ELSE CAST(SUM(CASE WHEN r.GolosCasa = r.GolosVisitante THEN 1 ELSE 0 END) AS FLOAT) / @TotalJogosFinalizados
        END                                                                          AS TaxaVitoriaX,
        CASE WHEN @TotalJogosFinalizados = 0 THEN 0
             ELSE CAST(SUM(CASE WHEN r.GolosVisitante > r.GolosCasa THEN 1 ELSE 0 END) AS FLOAT) / @TotalJogosFinalizados
        END                                                                          AS TaxaVitoria2
    FROM dbo.Jogos j
    LEFT JOIN dbo.Resultados r ON r.JogoId = j.Id AND j.Estado = 3
    LEFT JOIN dbo.Apostas a ON a.JogoId = j.Id
    WHERE j.TipoCompeticao = @TipoCompeticao;
END
GO

-- ----------------------------------------------------------------
-- sp_ConsultarOddsJogo
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarOddsJogo
    @CodigoJogo VARCHAR(20)
AS
BEGIN
    SET NOCOUNT ON;
    SELECT CodigoJogo, Odd1, OddX, Odd2
    FROM dbo.Jogos
    WHERE CodigoJogo = @CodigoJogo;
END
GO

-- ----------------------------------------------------------------
-- sp_ObterPagamentosPendentes
--   Devolve apostas com PagamentoPendente=1 para um jogo (ou todos).
--   Usado pela ApiApostas apos transicoes de estado.
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ObterPagamentosPendentes
    @CodigoJogo VARCHAR(20) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    SELECT a.Id, j.CodigoJogo, a.UtilizadorId, a.TipoAposta, a.MontanteApostado, a.OddMomento, a.Estado,
           CAST(a.MontanteApostado * a.OddMomento AS DECIMAL(10,2)) AS PremioPotencial
    FROM dbo.Apostas a
    JOIN dbo.Jogos j ON j.Id = a.JogoId
    WHERE a.PagamentoPendente = 1
      AND (@CodigoJogo IS NULL OR j.CodigoJogo = @CodigoJogo);
END
GO

-- ----------------------------------------------------------------
-- sp_MarcarPagamentoProcessado
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_MarcarPagamentoProcessado
    @ApostaId INT
AS
BEGIN
    SET NOCOUNT ON;
    UPDATE dbo.Apostas SET PagamentoPendente = 0 WHERE Id = @ApostaId;
END
GO

-- ================================================================
-- TABELA: EventosProcessados (idempotência de mensagens RabbitMQ)
-- ================================================================
BEGIN TRY
    BEGIN TRAN;
    IF OBJECT_ID(N'dbo.EventosProcessados', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.EventosProcessados (
            EventId       UNIQUEIDENTIFIER NOT NULL CONSTRAINT pk_eventos_processados PRIMARY KEY,
            CodigoJogo    VARCHAR(20)      NOT NULL,
            ProcessadoEm  DATETIME2        NOT NULL CONSTRAINT df_eventos_processados_data DEFAULT (SYSUTCDATETIME())
        );
        PRINT 'Tabela EventosProcessados criada.';
    END
    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'Erro ao criar tabela EventosProcessados: ' + ERROR_MESSAGE();
END CATCH
GO

-- ================================================================
-- TABELA: LogAdmin (auditoria de acoes administrativas)
-- ================================================================
BEGIN TRY
    BEGIN TRAN;

    IF OBJECT_ID(N'dbo.LogAdmin', N'U') IS NULL
    BEGIN
        CREATE TABLE dbo.LogAdmin (
            Id            INT IDENTITY(1,1) PRIMARY KEY,
            AdminId       NVARCHAR(450) NOT NULL,
            UtilizadorId  NVARCHAR(450) NULL,
            Acao          NVARCHAR(100) NOT NULL,
            Detalhe       NVARCHAR(MAX) NULL,
            DataHora      DATETIME2     NOT NULL CONSTRAINT df_logadmin_data DEFAULT (GETUTCDATE())
        );
        CREATE INDEX IX_LogAdmin_AdminId      ON dbo.LogAdmin (AdminId);
        CREATE INDEX IX_LogAdmin_UtilizadorId ON dbo.LogAdmin (UtilizadorId);
        PRINT 'tabela LogAdmin criada.';
    END
    ELSE PRINT 'tabela LogAdmin ja existe.';

    COMMIT TRAN;
END TRY
BEGIN CATCH
    IF @@TRANCOUNT > 0 ROLLBACK TRAN;
    PRINT 'erro tabela LogAdmin: ' + ERROR_MESSAGE();
END CATCH;
GO

-- ----------------------------------------------------------------
-- sp_ConsultarUtilizadores  (admin: lista users sem cross-DB)
--   O saldo e obtido separadamente via ApiPagamentos.
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarUtilizadores
AS
BEGIN
    SET NOCOUNT ON;
    SELECT
        u.Id,
        u.NomeCompleto,
        u.Email,
        u.DataRegisto,
        CASE WHEN ur.UserId IS NOT NULL THEN 1 ELSE 0 END AS IsAdmin
    FROM dbo.AspNetUsers u
    LEFT JOIN dbo.AspNetUserRoles ur ON ur.UserId = u.Id
        AND ur.RoleId = (SELECT Id FROM dbo.AspNetRoles WHERE NormalizedName = 'ADMIN')
    ORDER BY u.DataRegisto DESC;
END
GO

-- ----------------------------------------------------------------
-- sp_LogarAcaoAdmin  — regista accoes administrativas (sem ops financeiras)
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_LogarAcaoAdmin
    @AdminId     NVARCHAR(450),
    @UtilizadorId NVARCHAR(450) = NULL,
    @Acao        NVARCHAR(100),
    @Detalhe     NVARCHAR(MAX) = NULL
AS
BEGIN
    SET NOCOUNT ON;
    INSERT INTO dbo.LogAdmin (AdminId, UtilizadorId, Acao, Detalhe)
    VALUES (@AdminId, @UtilizadorId, @Acao, @Detalhe);
END
GO

-- ----------------------------------------------------------------
-- sp_ConsultarLogAdmin
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_ConsultarLogAdmin
    @AdminId     NVARCHAR(450) = NULL,
    @UtilizadorId NVARCHAR(450) = NULL,
    @Pagina      INT           = NULL
AS
BEGIN
    SET NOCOUNT ON;
    DECLARE @Offset INT = (ISNULL(@Pagina, 1) - 1) * 50;
    SELECT
        l.Id, l.AdminId, l.UtilizadorId, l.Acao, l.Detalhe, l.DataHora,
        a.Email AS AdminEmail,
        u.Email AS UtilizadorEmail
    FROM dbo.LogAdmin l
    LEFT JOIN dbo.AspNetUsers a ON a.Id = l.AdminId
    LEFT JOIN dbo.AspNetUsers u ON u.Id = l.UtilizadorId
    WHERE (@AdminId      IS NULL OR l.AdminId      = @AdminId)
      AND (@UtilizadorId IS NULL OR l.UtilizadorId = @UtilizadorId)
    ORDER BY l.DataHora DESC
    OFFSET @Offset ROWS FETCH NEXT 50 ROWS ONLY;
END
GO

-- ================================================================
-- UTILIZADOR E PERMISSOES
-- ================================================================
IF SUSER_ID('apostas') IS NULL
BEGIN
    PRINT '!!! LOGIN [apostas] NAO EXISTE. Rode apostas_credentials.example.sql primeiro. !!!';
END
ELSE
BEGIN
    IF USER_ID('apostas') IS NULL
    BEGIN
        CREATE USER [apostas] FOR LOGIN [apostas];
        PRINT 'utilizador apostas criado em rr1x24-apostas.';
    END
    ELSE
        PRINT 'utilizador apostas ja existe.';

    GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.Jogos      TO [apostas];
    GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.Resultados TO [apostas];
    GRANT SELECT, INSERT, UPDATE, DELETE ON dbo.Apostas    TO [apostas];
    GRANT EXECUTE ON dbo.sp_InserirJogo            TO [apostas];
    GRANT EXECUTE ON dbo.sp_AtualizarJogo          TO [apostas];
    GRANT EXECUTE ON dbo.sp_RemoverJogo            TO [apostas];
    GRANT EXECUTE ON dbo.sp_ConsultarJogos         TO [apostas];
    GRANT EXECUTE ON dbo.sp_ConsultarJogo          TO [apostas];
    GRANT EXECUTE ON dbo.sp_ResolverApostasJogo    TO [apostas];
    GRANT EXECUTE ON dbo.sp_InserirAposta          TO [apostas];
    GRANT EXECUTE ON dbo.sp_CancelarAposta         TO [apostas];
    GRANT EXECUTE ON dbo.sp_ConsultarApostas       TO [apostas];
    GRANT EXECUTE ON dbo.sp_ConsultarAposta        TO [apostas];
    GRANT EXECUTE ON dbo.sp_InserirResultado       TO [apostas];
    GRANT EXECUTE ON dbo.sp_ConsultarResultadoJogo TO [apostas];
    GRANT EXECUTE ON dbo.sp_ConsultarOddsJogo      TO [apostas];
    GRANT EXECUTE ON dbo.sp_EstatisticasJogo       TO [apostas];
    GRANT EXECUTE ON dbo.sp_EstatisticasCompeticao TO [apostas];
    GRANT EXECUTE ON dbo.sp_ConsultarUtilizadores     TO [apostas];
    GRANT EXECUTE ON dbo.sp_LogarAcaoAdmin            TO [apostas];
    GRANT EXECUTE ON dbo.sp_ObterPagamentosPendentes  TO [apostas];
    GRANT EXECUTE ON dbo.sp_MarcarPagamentoProcessado TO [apostas];
    GRANT EXECUTE ON dbo.sp_RemoverAposta             TO [apostas];
    GRANT EXECUTE ON dbo.sp_ConsultarLogAdmin         TO [apostas];
    GRANT SELECT, INSERT ON dbo.LogAdmin              TO [apostas];
    -- Acesso direto as tabelas Identity (EF Core Identity nao usa SPs)
    ALTER ROLE db_datareader ADD MEMBER [apostas];
    ALTER ROLE db_datawriter ADD MEMBER [apostas];
    PRINT 'permissoes atribuidas ao utilizador apostas.';
END
GO

-- Nota: o login [apostas] nao tem mais acesso directo a rr1x24-pagamentos.
-- A comunicacao entre ApiApostas e ApiPagamentos e feita via HTTP (X-Internal-Key).
USE [rr1x24-apostas];
GO
