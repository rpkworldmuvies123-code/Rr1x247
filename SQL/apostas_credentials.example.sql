-- Rodar este ficheiro antes de apostas_setup.sql e pagamentos_setup.sql
-- Alterar as passwords antes de usar

USE [master];
GO

IF SUSER_ID('apostas') IS NULL
BEGIN
    CREATE LOGIN [apostas] WITH PASSWORD = 'CHANGE_ME_STRONG_PASSWORD_FOR_BETS_DB';
    PRINT 'login [apostas] criado.';
END
ELSE
    PRINT 'login [apostas] ja existe.';
GO

IF SUSER_ID('pagamentos') IS NULL
BEGIN
    CREATE LOGIN [pagamentos] WITH PASSWORD = 'CHANGE_ME_STRONG_PASSWORD_FOR_PAYMENTS_DB';
    PRINT 'login [pagamentos] criado.';
END
ELSE
    PRINT 'login [pagamentos] ja existe.';
GO
