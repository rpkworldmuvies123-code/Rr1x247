#!/usr/bin/env bash
# ============================================================
#  BetStrike — Abre sessão tmux com layout de apresentação
#
#  Layout:
#  ┌─────────────────────┬──────────────────────┐
#  │                     │  logs ApiApostas      │
#  │   DEMO (demo.sh)    │  (Kafka + RabbitMQ)   │
#  │                     ├──────────────────────┤
#  │                     │  logs Analytics       │
#  └─────────────────────┴──────────────────────┘
#
#  Uso: ./demo-tmux.sh
#  Sair da sessão tmux: Ctrl+B → D  (detach)
#  Matar sessão:        tmux kill-session -t betstrike
# ============================================================

SESSION="betstrike"
REPO="$(cd "$(dirname "$0")" && pwd)"

# Matar sessão anterior se existir
tmux kill-session -t "$SESSION" 2>/dev/null || true

# Criar sessão nova (sem abrir janela ainda)
tmux new-session -d -s "$SESSION" -x 220 -y 50

# Painel principal (esquerda) — demo
tmux send-keys -t "$SESSION" "cd $REPO && clear && ./demo.sh" ""

# Painel direita cima — logs ApiApostas
tmux split-window -t "$SESSION" -h -p 40
tmux send-keys -t "$SESSION" \
  "cd $REPO && docker compose logs -f apiapostas 2>&1 | grep --line-buffered -i 'kafka\\|rabbit\\|resolv\\|sweeper\\|idempot\\|jogo\\|aposta'" Enter

# Painel direita baixo — logs Analytics
tmux split-window -t "$SESSION" -v -p 50
tmux send-keys -t "$SESSION" \
  "cd $REPO && docker compose logs -f analyticsservice 2>&1 | grep --line-buffered -i 'alerta\\|kafka\\|bet\\|game\\|sweeper'" Enter

# Voltar para o painel da demo
tmux select-pane -t "$SESSION:0.0"

# Títulos dos painéis
tmux set-option -t "$SESSION" pane-border-status top
tmux select-pane -t "$SESSION:0.0" -T "  DEMO  "
tmux select-pane -t "$SESSION:0.1" -T "  ApiApostas — Logs  "
tmux select-pane -t "$SESSION:0.2" -T "  AnalyticsService — Logs  "

# Cor de fundo para distinguir painéis (opcional)
tmux set-window-option -t "$SESSION" window-status-current-style "fg=cyan,bold"

echo ""
echo "╔══════════════════════════════════════════════╗"
echo "║  Sessão tmux 'betstrike' criada              ║"
echo "╚══════════════════════════════════════════════╝"
echo ""
echo "  A entrar na sessão..."
echo "  Para sair sem fechar:  Ctrl+B → D"
echo "  Para fechar tudo:      tmux kill-session -t betstrike"
echo ""

# Entrar na sessão
tmux attach-session -t "$SESSION"
