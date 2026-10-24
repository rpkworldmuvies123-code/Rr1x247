#!/bin/bash
set -e

SERVER="${DB_HOST:-mssql_db},${DB_PORT:-1433}"

# sqlcmd path differs between mssql-tools and mssql-tools18
if [ -f "/opt/mssql-tools18/bin/sqlcmd" ]; then
    SQLCMD="/opt/mssql-tools18/bin/sqlcmd"
elif [ -f "/opt/mssql-tools/bin/sqlcmd" ]; then
    SQLCMD="/opt/mssql-tools/bin/sqlcmd"
else
    echo "[db-init] ERRO: sqlcmd nao encontrado."
    exit 1
fi

echo "[db-init] Servidor: $SERVER"

# Validar variáveis obrigatórias
ERROS=0
for VAR in MSSQL_SA_PASSWORD RESULTADOS_DB_PASSWORD APOSTAS_DB_PASSWORD PAGAMENTOS_DB_PASSWORD; do
    VAL="${!VAR}"
    if [ -z "$VAL" ]; then
        echo "[db-init] ERRO: variavel $VAR nao esta definida no .env."
        ERROS=1
    elif [ "${#VAL}" -lt 8 ]; then
        echo "[db-init] ERRO: variavel $VAR tem menos de 8 caracteres (requisito SQL Server)."
        ERROS=1
    fi
done
[ "$ERROS" -eq 1 ] && exit 1

echo "[db-init] Aguardando SQL Server..."

RETRIES=30
for i in $(seq 1 $RETRIES); do
    if "$SQLCMD" -S "$SERVER" -U sa -P "$MSSQL_SA_PASSWORD" -C -Q "SELECT 1" > /dev/null 2>&1; then
        echo "[db-init] SQL Server pronto."
        break
    fi
    if [ "$i" -eq "$RETRIES" ]; then
        echo "[db-init] ERRO: SQL Server nao respondeu apos $RETRIES tentativas."
        exit 1
    fi
    echo "[db-init] Tentativa $i/$RETRIES..."
    sleep 2
done

run_sql() {
    "$SQLCMD" -S "$SERVER" -U sa -P "$MSSQL_SA_PASSWORD" -C -I -b "$@"
}

# Criar logins com as passwords do .env (idempotente)
echo "[db-init] Criando logins..."
cat > /tmp/logins.sql << EOF
USE [master];
GO
IF SUSER_ID(N'resultados') IS NULL
BEGIN
    CREATE LOGIN [resultados] WITH PASSWORD = N'${RESULTADOS_DB_PASSWORD}';
    PRINT 'login [resultados] criado.';
END
ELSE PRINT 'login [resultados] ja existe.';
GO
IF SUSER_ID(N'apostas') IS NULL
BEGIN
    CREATE LOGIN [apostas] WITH PASSWORD = N'${APOSTAS_DB_PASSWORD}';
    PRINT 'login [apostas] criado.';
END
ELSE PRINT 'login [apostas] ja existe.';
GO
IF SUSER_ID(N'pagamentos') IS NULL
BEGIN
    CREATE LOGIN [pagamentos] WITH PASSWORD = N'${PAGAMENTOS_DB_PASSWORD}';
    PRINT 'login [pagamentos] criado.';
END
ELSE PRINT 'login [pagamentos] ja existe.';
GO
EOF
run_sql -i /tmp/logins.sql

echo "[db-init] Configurando rr1x24-resultados..."
run_sql -i /sql/setup.sql

echo "[db-init] Configurando rr1x24-pagamentos..."
run_sql -i /sql/pagamentos_setup.sql

echo "[db-init] Configurando rr1x24-apostas..."
run_sql -i /sql/apostas_setup.sql

echo "[db-init] Inicializacao concluida com sucesso."
