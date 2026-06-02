#!/usr/bin/env bash
# ============================================================
#  BetStrike Parte 2 — Demo Script
#  Uso: ./demo.sh
#  Pré-requisito: docker compose up (stack toda healthy)
# ============================================================

set -e
REPO="$(cd "$(dirname "$0")" && pwd)"
cd "$REPO"

# ── Cores ────────────────────────────────────────────────────
B='\033[1m'; R='\033[0m'
CYAN='\033[1;36m'; GREEN='\033[1;32m'; YELLOW='\033[1;33m'
RED='\033[1;31m'; DIM='\033[2m'; PURPLE='\033[1;35m'

# ── Variáveis (lidas do .env) ─────────────────────────────────
export $(grep -v '^#' .env | grep -v '^$' | xargs)
API_KEY="$RESULTADOS_API_KEY"
SA_PASS="$MSSQL_SA_PASSWORD"
RMQ_PASS="$RABBITMQ_PASSWORD"

RESULTADOS="http://localhost:5204"
APOSTAS="http://localhost:5141"
ANALYTICS="http://localhost:5400"

# ── Helpers ──────────────────────────────────────────────────
step() {
  echo -e "\n${CYAN}╔══════════════════════════════════════════════════╗${R}"
  echo -e "${CYAN}║  $1${R}"
  echo -e "${CYAN}╚══════════════════════════════════════════════════╝${R}"
}

info()    { echo -e "${DIM}  ▸ $1${R}"; }
ok()      { echo -e "${GREEN}  ✓ $1${R}"; }
warn()    { echo -e "${YELLOW}  ⚠ $1${R}"; }
cmd()     { echo -e "${PURPLE}  \$ $1${R}"; }
result()  { echo -e "${B}  → $1${R}"; }

pause() {
  echo -e "\n${DIM}  [Enter para continuar]${R}"
  read -r
}

run() {
  cmd "$1"
  eval "$1"
  echo ""
}

runjq() {
  cmd "$1 | jq $2"
  eval "$1" | jq -r "$2" 2>/dev/null || eval "$1"
  echo ""
}

# ── Verificar stack ──────────────────────────────────────────
step "0. Verificar stack"
info "A verificar que todos os serviços estão healthy..."
echo ""
docker compose ps --format "table {{.Name}}\t{{.Status}}" | \
  grep -E "healthy|running|Up" || true
echo ""
if ! curl -sf "$APOSTAS/api/jogos" > /dev/null 2>&1; then
  echo -e "${RED}ApiApostas não responde. Correr: docker compose up -d${R}"
  exit 1
fi
ok "Stack OK"
pause

# ── 1. HEALTH CHECKS ────────────────────────────────────────
step "1. Health Checks — dependências de cada serviço"

info "ApiApostas → SQL + RabbitMQ + Kafka"
runjq "curl -s $APOSTAS/health" '.'
pause

info "ApiResultados → SQL + Kafka"
runjq "curl -s $RESULTADOS/health/ready" '.'
pause

info "AnalyticsService → Kafka + MetricsStore"
runjq "curl -s $ANALYTICS/health" '.'
pause

# ── 2. AUTH ──────────────────────────────────────────────────
step "2. Auth — utilizador de demo"

DEMO_EMAIL="demo-apres-$(date +%s)@betstrike.pt"
info "A registar $DEMO_EMAIL..."

REGISTER=$(curl -s -X POST "$APOSTAS/api/auth/registar" \
  -H "Content-Type: application/json" \
  -d "{\"email\":\"$DEMO_EMAIL\",\"password\":\"Demo123!\",\"nomeCompleto\":\"Demo Apresentação\"}")

TOKEN=$(echo "$REGISTER" | jq -r '.token // empty')
if [[ -z "$TOKEN" ]]; then
  # já existe — tentar login
  TOKEN=$(curl -s -X POST "$APOSTAS/api/auth/login" \
    -H "Content-Type: application/json" \
    -d "{\"email\":\"$DEMO_EMAIL\",\"password\":\"Demo123!\"}" | jq -r '.token')
fi

ok "Token obtido: ${TOKEN:0:30}..."
SALDO=$(curl -s "$APOSTAS/api/pagamentos/saldo" \
  -H "Authorization: Bearer $TOKEN" | jq -r '.SaldoAtual')
result "Saldo inicial: ${SALDO} €"
pause

# ── 3. PIPELINE KAFKA ────────────────────────────────────────
step "3. Pipeline Kafka — criar jogo em ApiResultados"

COD="FUT-2026-$(shuf -i 1000-9998 -n 1)"
info "Código do jogo: $COD"
info "A criar em ApiResultados (publica game-events no Kafka)..."

CREATE=$(curl -s -X POST "$RESULTADOS/api/jogos" \
  -H "Content-Type: application/json" \
  -H "X-API-Key: $API_KEY" \
  -d "{
    \"codigoJogo\": \"$COD\",
    \"equipaCasa\": \"Benfica\",
    \"equipaFora\": \"Porto\",
    \"tipoCompeticao\": \"Primeira Liga\",
    \"dataHoraInicio\": \"2026-12-15T20:00:00Z\",
    \"estadoAtual\": 1
  }")

echo "$CREATE" | jq '{codigoJogo, id}'
echo ""

info "A aguardar Kafka propagar para ApiApostas..."
for i in $(seq 1 10); do
  sleep 0.5
  STATUS=$(curl -s "$APOSTAS/api/jogos/$COD" \
    -H "Authorization: Bearer $TOKEN" | jq -r '.Estado // empty')
  if [[ -n "$STATUS" ]]; then
    ok "Kafka OK — jogo chegou a ApiApostas em ~${i}×0.5s"
    break
  fi
done

info "Jogo em ApiApostas:"
runjq "curl -s $APOSTAS/api/jogos/$COD -H 'Authorization: Bearer $TOKEN'" \
  '{CodigoJogo, Estado, EquipaCasa, EquipaFora, Odd1, OddX, Odd2}'
pause

# ── 4. APOSTA + ANALYTICS ────────────────────────────────────
step "4. Colocar aposta + ver Analytics"

info "A apostar €20 no 1 (Benfica ganha)..."
APOSTA=$(curl -s -X POST "$APOSTAS/api/apostas" \
  -H "Content-Type: application/json" \
  -H "Authorization: Bearer $TOKEN" \
  -d "{\"codigoJogo\":\"$COD\",\"tipoAposta\":\"1\",\"montanteApostado\":20}")

APOSTA_ID=$(echo "$APOSTA" | jq -r '.id')
ODD=$(echo "$APOSTA" | jq -r '.oddMomento')
PREMIO=$(echo "$ODD * 20" | bc)

ok "Aposta id=$APOSTA_ID odd=$ODD prémio potencial=€$PREMIO"
result "Saldo após debitado: $(curl -s "$APOSTAS/api/pagamentos/saldo" \
  -H "Authorization: Bearer $TOKEN" | jq -r '.SaldoAtual') €"
echo ""

info "Analytics — jogo específico:"
runjq "curl -s $ANALYTICS/api/analytics/jogos/$COD" \
  '{codigoJogo, volumeApostado, exposicaoFinanceira}'

info "Analytics — resumo global:"
runjq "curl -s $ANALYTICS/api/analytics/resumo" \
  '{apostas, utilizadoresAtivos5Min, volumeTotal, margemPlataforma}'
pause

# ── 5. RABBITMQ — RESOLUÇÃO ─────────────────────────────────
step "5. RabbitMQ — Finalizar jogo e resolver aposta"

info "A fazer jogo → EmCurso (1-0)..."
PATCH_EC=$(curl -s -X PATCH "$RESULTADOS/api/jogos/$COD" \
  -H "Content-Type: application/json" \
  -H "X-API-Key: $API_KEY" \
  -d '{"estadoAtual":2,"golosCasa":1,"golosVisitante":0}')
ESTADO_EC=$(echo "$PATCH_EC" | jq -r '.estadoAtual // .EstadoAtual // "erro"')
[[ "$ESTADO_EC" == "2" ]] && ok "Estado: 2 (EmCurso)" || warn "Resposta: $(echo $PATCH_EC | jq -c .)"

sleep 0.5
info "A fazer jogo → Finalizado — RabbitMQ publica bet-resolution..."
PATCH_FIN=$(curl -s -X PATCH "$RESULTADOS/api/jogos/$COD" \
  -H "Content-Type: application/json" \
  -H "X-API-Key: $API_KEY" \
  -d '{"estadoAtual":3}')
ESTADO_FIN=$(echo "$PATCH_FIN" | jq -r '.estadoAtual // .EstadoAtual // "erro"')
[[ "$ESTADO_FIN" == "3" ]] && ok "Estado: 3 (Finalizado) → mensagem publicada em RabbitMQ" \
  || warn "Resposta: $(echo $PATCH_FIN | jq -c .)"

echo ""
info "A aguardar BetResolutionWorker processar (~2s)..."
sleep 3

APOSTAS_LIST=$(curl -s "$APOSTAS/api/apostas" -H "Authorization: Bearer $TOKEN")
ESTADO_APOSTA=$(echo "$APOSTAS_LIST" | jq --argjson id "${APOSTA_ID:-0}" \
  '[.[] | select(.Id == $id)] | .[0] | {Id, Estado, PremioPotencial}')
echo "$ESTADO_APOSTA" | jq .

ESTADO_NUM=$(echo "$ESTADO_APOSTA" | jq -r '.Estado')
if [[ "$ESTADO_NUM" == "2" ]]; then
  ok "Aposta Ganha ✓"
else
  warn "Estado: $ESTADO_NUM (2=Ganha, 3=Perdida)"
fi

SALDO_FINAL=$(curl -s "$APOSTAS/api/pagamentos/saldo" \
  -H "Authorization: Bearer $TOKEN" | jq -r '.SaldoAtual')
result "Saldo final: ${SALDO_FINAL} € (foi: ${SALDO} €)"
pause

# ── 6. IDEMPOTÊNCIA ──────────────────────────────────────────
step "6. Idempotência — EventosProcessados"

info "Tabela de deduplicação de mensagens RabbitMQ:"
docker compose exec mssql_db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASS" -d "betstrike-apostas" -No \
  -Q "SELECT TOP 5 CAST(EventId AS VARCHAR(40)) AS EventId, CodigoJogo,
       CONVERT(VARCHAR(23), ProcessadoEm, 120) AS ProcessadoEm
      FROM dbo.EventosProcessados
      ORDER BY ProcessadoEm DESC"
echo ""
TOTAL=$(docker compose exec mssql_db /opt/mssql-tools18/bin/sqlcmd \
  -S localhost -U sa -P "$SA_PASS" -d "betstrike-apostas" -No \
  -Q "SET NOCOUNT ON; SELECT COUNT(*) FROM dbo.EventosProcessados" 2>/dev/null | grep -oP '\d+' | head -1)
ok "Total eventos únicos processados: $TOTAL"
pause

# ── 7. ANALYTICS FINAL ──────────────────────────────────────
step "7. Analytics — estado atual"

RESUMO=$(curl -s "$ANALYTICS/api/analytics/resumo")
echo "$RESUMO" | jq '{apostas, utilizadoresAtivos5Min, taxasVitoria, volumeTotal, margemPlataforma}'
echo ""

ALERTAS=$(curl -s "$ANALYTICS/api/analytics/alertas")
N_ALERTAS=$(echo "$ALERTAS" | jq 'length')
ok "$N_ALERTAS alertas registados"
if [[ "$N_ALERTAS" -gt 0 ]]; then
  echo "$ALERTAS" | jq 'group_by(.tipo) | map({tipo: .[0].tipo, count: length})'
fi
pause

# ── 8. MENU ──────────────────────────────────────────────────
step "8. O que mostrar a seguir?"
echo ""
echo -e "  ${B}[1]${R} Stress Test básico    (stress.mjs  — 40 apostas)"
echo -e "  ${B}[2]${R} Stress Test completo  (stress2.mjs — 105 apostas + alertas)"
echo -e "  ${B}[3]${R} Stress Test full      (stress3.mjs — 905 apostas)"
echo -e "  ${B}[4]${R} Resilience Test       (3 cenários de falha)"
echo -e "  ${B}[5]${R} Sair"
echo ""
echo -n "  Escolha: "
read -r CHOICE

case $CHOICE in
  1) node "$REPO/stress-test/stress.mjs" ;;
  2) node "$REPO/stress-test/stress2.mjs" ;;
  3) node "$REPO/stress-test/stress3.mjs" ;;
  4) node "$REPO/stress-test/resilience.mjs" ;;
  5) ;;
  *) echo "OK, a sair." ;;
esac

# ── Fim ──────────────────────────────────────────────────────
echo ""
echo -e "${CYAN}╔══════════════════════════════════════════════════╗${R}"
echo -e "${CYAN}║         Demo concluída — BetStrike Parte 2       ║${R}"
echo -e "${CYAN}╚══════════════════════════════════════════════════╝${R}"
echo ""
echo -e "  ${DIM}URLs úteis:${R}"
echo -e "  RabbitMQ UI  →  http://localhost:15672  (betstrike / $RMQ_PASS)"
echo -e "  Analytics    →  http://localhost:5400/api/analytics/resumo"
echo -e "  Frontend     →  http://localhost:4200  (admin@betstrike.pt / Admin123!)"
echo ""
