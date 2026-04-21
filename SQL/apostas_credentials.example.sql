-- Rodar este ficheiro antes de apostas_setup.sql e pagamentos_setup.sql
-- Alterar as passwords antes de usar

USE [master];
GO

IF SUSER_ID('apostas') IS NULL
BEGIN
    CREATE LOGIN [apostas] WITH PASSWORD = 'PASSWORD_APOSTAS_AQUI';
    PRINT 'login [apostas] criado.';
END
ELSE
    PRINT 'login [apostas] ja existe.';
GO

IF SUSER_ID('pagamentos') IS NULL
BEGIN
    CREATE LOGIN [pagamentos] WITH PASSWORD = 'PASSWORD_PAGAMENTOS_AQUI';
    PRINT 'login [pagamentos] criado.';
END
ELSE
    PRINT 'login [pagamentos] ja existe.';
GO
