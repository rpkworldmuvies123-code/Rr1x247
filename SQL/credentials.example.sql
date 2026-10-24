-- rodar este ficheiro e depois setup.sql
-- este ficheiro é um EXEMPLO, as passwords devem ser alteradas

USE [master];
GO

IF SUSER_ID('resultados') IS NULL
	BEGIN
		CREATE LOGIN [resultados] WITH PASSWORD = 'CHANGE_ME_STRONG_PASSWORD_FOR_RESULTS_DB';
		PRINT 'login [resultados] criado com sucesso.';
	END
ELSE
	BEGIN
	    PRINT 'login [resultados] ja existe.';
	END
	
GO