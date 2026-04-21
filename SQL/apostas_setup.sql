-- Pré-requisitos: apostas_credentials.example.sql + pagamentos_setup.sql

USE [master];
GO

IF DB_ID(N'betstrike-apostas') IS NULL
BEGIN
    CREATE DATABASE [betstrike-apostas];
    PRINT 'database betstrike-apostas criada.';
END
ELSE
    PRINT 'database betstrike-apostas ja existe.';
GO

USE [betstrike-apostas];
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
            CONSTRAINT UQ_Jogos_CodigoJogo   UNIQUE  (CodigoJogo),
            CONSTRAINT CK_Jogos_CodigoFormato CHECK  (CodigoJogo LIKE 'FUT-[0-9][0-9][0-9][0-9]-[0-9][0-9][0-9][0-9]'),
            CONSTRAINT CK_Jogos_Estado        CHECK  (Estado BETWEEN 1 AND 5),
            CONSTRAINT CK_Jogos_GolosCasa     CHECK  (GolosCasa >= 0),
            CONSTRAINT CK_Jogos_GolosVisitante CHECK (GolosVisitante >= 0)
        );
        PRINT 'tabela Jogos criada.';
    END
    ELSE PRINT 'tabela Jogos ja existe.';

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
            Id                INT IDENTITY(1,1) PRIMARY KEY,
            JogoId            INT           NOT NULL,
            UtilizadorId      NVARCHAR(450) NOT NULL,
            TipoAposta        CHAR(1)       NOT NULL,
            MontanteApostado  DECIMAL(10,2) NOT NULL,
            OddMomento        DECIMAL(10,4) NOT NULL,
            Estado            SMALLINT      NOT NULL CONSTRAINT df_apostas_estado DEFAULT (1),
            DataHoraRegisto   DATETIME2     NOT NULL CONSTRAINT df_apostas_data   DEFAULT (GETUTCDATE()),
            CONSTRAINT FK_Apostas_Jogos       FOREIGN KEY (JogoId) REFERENCES dbo.Jogos(Id),
            CONSTRAINT CK_Apostas_TipoAposta  CHECK (TipoAposta IN ('1','X','2')),
            CONSTRAINT CK_Apostas_Estado      CHECK (Estado BETWEEN 1 AND 4),
            CONSTRAINT CK_Apostas_Montante    CHECK (MontanteApostado > 0),
            CONSTRAINT CK_Apostas_Odd         CHECK (OddMomento > 1.0)
        );
        PRINT 'tabela Apostas criada.';
    END
    ELSE PRINT 'tabela Apostas ja existe.';

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

    -- Transitioning to Cancelado (4) or Adiado (5): annul all pending bets
    IF @Estado IN (4, 5)
    BEGIN
        UPDATE dbo.Apostas SET Estado = 4
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
        ISNULL((SELECT COUNT(*) FROM dbo.Apostas WHERE JogoId = j.Id AND Estado = 1), 0) AS ApostasPendentes,
        ISNULL((SELECT SUM(MontanteApostado) FROM dbo.Apostas WHERE JogoId = j.Id AND Estado = 1), 0) AS VolumeTotalApostado
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
        END
    WHERE JogoId = @JogoId AND Estado = 1;
END
GO

-- ----------------------------------------------------------------
-- sp_InserirAposta
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_InserirAposta
    @CodigoJogo       VARCHAR(20),
    @UtilizadorId     NVARCHAR(450),
    @TipoAposta       CHAR(1),
    @MontanteApostado DECIMAL(10,2),
    @OddMomento       DECIMAL(10,4),
    @ApostaId         INT OUTPUT
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    DECLARE @JogoId INT, @Estado SMALLINT;
    SELECT @JogoId = Id, @Estado = Estado FROM dbo.Jogos WHERE CodigoJogo = @CodigoJogo;

    IF @JogoId IS NULL     THROW 50007, 'Jogo nao encontrado.', 1;
    IF @Estado NOT IN (1,2) THROW 50008, 'Nao e permitido registar apostas em jogos com este estado.', 1;
    IF @TipoAposta NOT IN ('1','X','2') THROW 50009, 'O tipo de aposta deve ser 1, X ou 2.', 1;
    IF @MontanteApostado <= 0           THROW 50010, 'O montante apostado deve ser positivo.', 1;
    IF @OddMomento <= 1.0               THROW 50011, 'A odd deve ser superior a 1.0.', 1;

    DECLARE @SaldoAtual DECIMAL(10,2);
    SELECT @SaldoAtual = SaldoAtual
    FROM [betstrike-pagamentos].dbo.Saldo_Utilizador
    WHERE UtilizadorId = @UtilizadorId;

    IF @SaldoAtual IS NULL        THROW 50012, 'Registo de saldo nao encontrado para o utilizador.', 1;
    IF @SaldoAtual < @MontanteApostado THROW 50013, 'Saldo insuficiente para realizar esta aposta.', 1;

    INSERT INTO dbo.Apostas (JogoId, UtilizadorId, TipoAposta, MontanteApostado, OddMomento, Estado, DataHoraRegisto)
    VALUES (@JogoId, @UtilizadorId, @TipoAposta, @MontanteApostado, @OddMomento, 1, GETUTCDATE());
    SET @ApostaId = SCOPE_IDENTITY();

    UPDATE [betstrike-pagamentos].dbo.Saldo_Utilizador
    SET SaldoAtual = SaldoAtual - @MontanteApostado,
        DataHoraAtualizacao = GETUTCDATE()
    WHERE UtilizadorId = @UtilizadorId;

    INSERT INTO [betstrike-pagamentos].dbo.Transacoes
        (ApostaId, UtilizadorId, TipoTransacao, Valor, DataHora, Estado)
    VALUES (@ApostaId, @UtilizadorId, 'AP', @MontanteApostado, GETUTCDATE(), 'Processada');

    COMMIT TRANSACTION;
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

    IF @JogoId IS NULL      THROW 50014, N'Aposta não encontrada.', 1;
    IF @IsAdmin = 0 AND @DonoDaAposta != @UtilizadorId
                            THROW 50016, N'Não tem permissão para cancelar esta aposta.', 1;
    IF @EstadoAposta != 1   THROW 50015, N'Apenas apostas pendentes podem ser canceladas.', 1;
    IF @EstadoJogo != 1     THROW 50015, N'Só é permitido cancelar apostas enquanto o jogo está no estado Agendado.', 1;

    -- Trigger trg_Apostas_ResolucaoFinanceira handles the RE transaction + balance refund
    UPDATE dbo.Apostas SET Estado = 4 WHERE Id = @ApostaId;
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
        a.Id, a.JogoId, j.CodigoJogo, j.EquipaCasa, j.EquipaFora,
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
-- sp_CriarSaldoUtilizador  (chamado apos Identity criar o user)
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_CriarSaldoUtilizador
    @UtilizadorId NVARCHAR(450)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF NOT EXISTS (SELECT 1 FROM [betstrike-pagamentos].dbo.Saldo_Utilizador WHERE UtilizadorId = @UtilizadorId)
    BEGIN
        INSERT INTO [betstrike-pagamentos].dbo.Saldo_Utilizador (UtilizadorId, SaldoAtual)
        VALUES (@UtilizadorId, 50.00);

        INSERT INTO [betstrike-pagamentos].dbo.Transacoes
            (UtilizadorId, TipoTransacao, Valor, Estado)
        VALUES (@UtilizadorId, 'DE', 50.00, 'Processada');
    END

    COMMIT TRANSACTION;
END
GO

-- ----------------------------------------------------------------
-- sp_DepositarSaldo
-- ----------------------------------------------------------------
CREATE OR ALTER PROCEDURE dbo.sp_DepositarSaldo
    @UtilizadorId NVARCHAR(450),
    @Valor        DECIMAL(10,2)
AS
BEGIN
    SET NOCOUNT ON; SET XACT_ABORT ON;
    BEGIN TRANSACTION;

    IF @Valor <= 0 THROW 50010, 'O valor do deposito deve ser positivo.', 1;

    IF NOT EXISTS (SELECT 1 FROM [betstrike-pagamentos].dbo.Saldo_Utilizador WHERE UtilizadorId = @UtilizadorId)
        THROW 50012, 'Registo de saldo nao encontrado para o utilizador.', 1;

    UPDATE [betstrike-pagamentos].dbo.Saldo_Utilizador
    SET SaldoAtual = SaldoAtual + @Valor,
        DataHoraAtualizacao = GETUTCDATE()
    WHERE UtilizadorId = @UtilizadorId;

    INSERT INTO [betstrike-pagamentos].dbo.Transacoes
        (UtilizadorId, TipoTransacao, Valor, Estado)
    VALUES (@UtilizadorId, 'DE', @Valor, 'Processada');

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
    SELECT SaldoAtual, DataHoraAtualizacao
    FROM [betstrike-pagamentos].dbo.Saldo_Utilizador
    WHERE UtilizadorId = @UtilizadorId;
END
GO

-- ================================================================
-- TRIGGER: resolucao financeira (Ganha/Anulada -> Pagamentos)
-- ================================================================
IF OBJECT_ID(N'dbo.trg_Apostas_ResolucaoFinanceira', N'TR') IS NOT NULL
    DROP TRIGGER dbo.trg_Apostas_ResolucaoFinanceira;
GO

CREATE TRIGGER dbo.trg_Apostas_ResolucaoFinanceira
ON dbo.Apostas
AFTER UPDATE
AS
BEGIN
    SET NOCOUNT ON;
    IF NOT UPDATE(Estado) RETURN;

    -- Apostas Ganhas (Estado -> 2): PG + creditar saldo
    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.Id = d.Id WHERE i.Estado = 2 AND d.Estado != 2)
    BEGIN
        INSERT INTO [betstrike-pagamentos].dbo.Transacoes
            (ApostaId, UtilizadorId, TipoTransacao, Valor, DataHora, Estado)
        SELECT i.Id, i.UtilizadorId, 'PG',
               CAST(i.MontanteApostado * i.OddMomento AS DECIMAL(10,2)),
               GETUTCDATE(), 'Processada'
        FROM inserted i
        JOIN deleted d ON i.Id = d.Id
        WHERE i.Estado = 2 AND d.Estado != 2;

        UPDATE su
        SET su.SaldoAtual = su.SaldoAtual + agg.Premio,
            su.DataHoraAtualizacao = GETUTCDATE()
        FROM [betstrike-pagamentos].dbo.Saldo_Utilizador su
        JOIN (
            SELECT i.UtilizadorId,
                   SUM(CAST(i.MontanteApostado * i.OddMomento AS DECIMAL(10,2))) AS Premio
            FROM inserted i JOIN deleted d ON i.Id = d.Id
            WHERE i.Estado = 2 AND d.Estado != 2
            GROUP BY i.UtilizadorId
        ) agg ON su.UtilizadorId = agg.UtilizadorId;
    END

    -- Apostas Anuladas (Estado -> 4): RE + reembolsar saldo
    IF EXISTS (SELECT 1 FROM inserted i JOIN deleted d ON i.Id = d.Id WHERE i.Estado = 4 AND d.Estado != 4)
    BEGIN
        INSERT INTO [betstrike-pagamentos].dbo.Transacoes
            (ApostaId, UtilizadorId, TipoTransacao, Valor, DataHora, Estado)
        SELECT i.Id, i.UtilizadorId, 'RE',
               i.MontanteApostado,
               GETUTCDATE(), 'Processada'
        FROM inserted i
        JOIN deleted d ON i.Id = d.Id
        WHERE i.Estado = 4 AND d.Estado != 4;

        UPDATE su
        SET su.SaldoAtual = su.SaldoAtual + agg.Reembolso,
            su.DataHoraAtualizacao = GETUTCDATE()
        FROM [betstrike-pagamentos].dbo.Saldo_Utilizador su
        JOIN (
            SELECT i.UtilizadorId,
                   SUM(i.MontanteApostado) AS Reembolso
            FROM inserted i JOIN deleted d ON i.Id = d.Id
            WHERE i.Estado = 4 AND d.Estado != 4
            GROUP BY i.UtilizadorId
        ) agg ON su.UtilizadorId = agg.UtilizadorId;
    END
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
        PRINT 'utilizador apostas criado em betstrike-apostas.';
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
    GRANT EXECUTE ON dbo.sp_EstatisticasJogo       TO [apostas];
    GRANT EXECUTE ON dbo.sp_EstatisticasCompeticao TO [apostas];
    GRANT EXECUTE ON dbo.sp_CriarSaldoUtilizador   TO [apostas];
    GRANT EXECUTE ON dbo.sp_DepositarSaldo         TO [apostas];
    GRANT EXECUTE ON dbo.sp_ConsultarSaldo         TO [apostas];
    -- Acesso direto as tabelas Identity (EF Core Identity nao usa SPs)
    ALTER ROLE db_datareader ADD MEMBER [apostas];
    ALTER ROLE db_datawriter ADD MEMBER [apostas];
    -- Cross-DB access to Pagamentos (login-level permission)
    PRINT 'permissoes atribuidas ao utilizador apostas.';
END
GO

-- Permitir ao login apostas aceder ao betstrike-pagamentos
USE [betstrike-pagamentos];
GO
IF SUSER_ID('apostas') IS NOT NULL AND USER_ID('apostas') IS NULL
BEGIN
    CREATE USER [apostas] FOR LOGIN [apostas];
    GRANT SELECT, INSERT, UPDATE ON dbo.Saldo_Utilizador TO [apostas];
    GRANT SELECT, INSERT        ON dbo.Transacoes        TO [apostas];
    PRINT 'utilizador apostas criado em betstrike-pagamentos com permissoes cross-db.';
END
GO
