/**
 * BetStrike Stress Test v4 — 1000 apostas/segundo durante 30 segundos
 *
 * Alvo: ~30 000 apostas totais, throughput sustentado ≥1000/s
 *
 * Arquitetura:
 *   - 500 utilizadores, topped up a 350€ cada
 *   - 60 jogos abertos (Agendado) durante toda a fase de betting
 *   - Pool de concorrência deslizante: mantém N requests em voo ao mesmo tempo
 *   - Telemetria por janela de 1s — mostra throughput real a cada segundo
 *   - Após 30s de betting: finalizar todos os jogos → RabbitMQ resolve ~30k apostas
 */

const RESULTADOS_URL = 'http://localhost:5204';
const APOSTAS_URL    = 'http://localhost:5141';
const ANALYTICS_URL  = 'http://localhost:5400';
const API_KEY        = 'Ra1MTLOcHOaJL8EXlAaKQ8PjGMrccSgr5w4yxg0c6QNRb1zP0WVAj81KcX6uQV0T';
const ADMIN_EMAIL    = 'admin@betstrike.pt';
const ADMIN_PASSWORD = 'Admin123!';

const TOTAL_USERS     = 500;
const TOTAL_GAMES     = 60;
const TOPUP           = 300;     // € extra por user (50 inicial + 300 = 350€ → 70 bets a 5€)
const MONTANTE        = 5;       // € por aposta
const CONCURRENCY     = 400;     // requests em voo em simultâneo
const BETTING_SECS    = 30;      // duração da fase de betting
const YEAR            = '2028';  // evita colisões com runs anteriores

const RUN_ID = String(Date.now()).slice(-4);
const BASE   = RUN_ID.slice(0, 2);

const start = Date.now();
const c = (code, s) => `\x1b[${code}m${s}\x1b[0m`;
const elapsed = () => `${((Date.now() - start) / 1000).toFixed(1)}s`.padStart(7);
function log(tag, msg) {
  const colors = { SETUP:'90', JOGO:'36', KAFKA:'32', BET:'33', RABBIT:'35', RESOLV:'32', STATS:'96', WARN:'93', ERR:'91', INFO:'37' };
  console.log(`[${elapsed()}] ${c(colors[tag]||'37', `[${tag.padEnd(6)}]`)} ${msg}`);
}

async function fetchJSON(url, opts = {}) {
  const r = await fetch(url, opts);
  const text = await r.text();
  let body; try { body = JSON.parse(text); } catch { body = text; }
  return { status: r.status, body };
}
function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

// Pool de concorrência deslizante — mantém exatamente CONCURRENCY requests em voo
async function concurrentPool(tasks, concurrency, onResult) {
  const results = [];
  let i = 0;

  async function runNext() {
    if (i >= tasks.length) return;
    const idx = i++;
    const result = await tasks[idx]().catch(e => ({ error: e }));
    if (onResult) onResult(result, idx);
    results.push(result);
    return runNext();
  }

  const workers = Array.from({ length: Math.min(concurrency, tasks.length) }, runNext);
  await Promise.all(workers);
  return results;
}

// ── Auth ──────────────────────────────────────────────────────────────────────
async function getToken(email, password, nome = 'Stress User') {
  let r = await fetchJSON(`${APOSTAS_URL}/api/auth/registar`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, nomeCompleto: nome }),
  });
  if (r.status === 200) return { token: r.body.token, id: r.body.utilizadorId };
  r = await fetchJSON(`${APOSTAS_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  if (r.status === 200) return { token: r.body.token, id: r.body.utilizadorId };
  throw new Error(`auth ${email}: ${r.status}`);
}

async function topUpBatch(adminToken, users) {
  let ok = 0;
  await concurrentPool(
    users.map(u => () => fetchJSON(`${APOSTAS_URL}/api/admin/utilizadores/${u.id}/saldo`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` },
      body: JSON.stringify({ valor: TOPUP, motivo: `s4-${RUN_ID}` }),
    })),
    100,
    r => { if (r?.status === 200) ok++; }
  );
  return ok;
}

async function criarJogoBatch(codigos) {
  let ok = 0;
  const equipas = [
    'Benfica','Porto','Sporting','Braga','Vitoria','Guimaraes',
    'Famalicao','Boavista','Estoril','Arouca','Casa Pia','Gil Vicente',
    'Farense','Portimonense','Estrela','Nacional','Moreirense','Vizela',
    'Penafiel','AVS',
  ];
  await concurrentPool(
    codigos.map((cod, i) => () => fetchJSON(`${RESULTADOS_URL}/api/jogos`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', 'X-API-Key': API_KEY },
      body: JSON.stringify({
        codigoJogo: cod,
        equipaCasa: equipas[(i * 2) % equipas.length],
        equipaFora: equipas[(i * 2 + 1) % equipas.length],
        tipoCompeticao: 'Primeira Liga',
        dataHoraInicio: '2028-01-20T20:00:00Z',
        estadoAtual: 1,
      }),
    })),
    30,
    r => { if (r?.status === 201) ok++; }
  );
  return ok;
}

async function aguardarTodosJogos(codigos, token) {
  const pendentes = new Set(codigos);
  for (let t = 0; t < 30 && pendentes.size > 0; t++) {
    const lista = [...pendentes];
    await Promise.all(lista.map(async cod => {
      const r = await fetchJSON(`${APOSTAS_URL}/api/jogos/${cod}`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (r.status === 200) pendentes.delete(cod);
    }));
    if (pendentes.size > 0) await sleep(500);
  }
  return codigos.length - pendentes.size;
}

// ── Main ──────────────────────────────────────────────────────────────────────
async function main() {
  const totalBets = TOTAL_USERS * TOTAL_GAMES;

  console.log(c('1;36', '══════════════════════════════════════════════════'));
  console.log(c('1',    ` BetStrike Stress Test v4 — 1000 APOSTAS/SEGUNDO`));
  console.log(`         ${TOTAL_USERS} users × ${TOTAL_GAMES} jogos = ${c('1;33', totalBets + ' apostas')}`);
  console.log(`         concorrência: ${CONCURRENCY}  |  betting: ${BETTING_SECS}s  |  run: ${RUN_ID}`);
  console.log(c('1;36', '══════════════════════════════════════════════════') + '\n');

  // ── Fase 1: Auth em paralelo ────────────────────────────────────────────────
  log('SETUP', `A registar/login ${TOTAL_USERS} users em paralelo (batch de 100)...`);
  const t1 = Date.now();
  const pw = `S4pass${RUN_ID}!`;
  const [adminAuth, ...userResults] = await Promise.all([
    getToken(ADMIN_EMAIL, ADMIN_PASSWORD, 'Admin'),
    ...Array.from({ length: TOTAL_USERS }, (_, i) =>
      getToken(`s4${RUN_ID}u${String(i).padStart(3,'0')}@betstrike.pt`, pw)
    ),
  ]);
  const users = userResults.filter(u => u?.token);
  log('SETUP', `${users.length} users em ${Date.now() - t1}ms`);

  // ── Fase 2: Top-up em batch ─────────────────────────────────────────────────
  log('SETUP', `Top-up de ${users.length} × ${TOPUP}€ em paralelo...`);
  const t2 = Date.now();
  const topups = await topUpBatch(adminAuth.token, users);
  log('SETUP', `${topups}/${users.length} top-ups OK em ${Date.now() - t2}ms  → saldo ~${50 + TOPUP}€/user`);

  // ── Fase 3: Criar jogos ─────────────────────────────────────────────────────
  const codigos = Array.from({ length: TOTAL_GAMES }, (_, i) =>
    `FUT-${YEAR}-${BASE}${String(i + 1).padStart(2, '0')}`
  );
  log('JOGO', `A criar ${TOTAL_GAMES} jogos em paralelo...`);
  const t3 = Date.now();
  const jogosOk = await criarJogoBatch(codigos);
  log('JOGO', `${jogosOk}/${TOTAL_GAMES} criados em ${Date.now() - t3}ms`);

  // ── Fase 4: Aguardar Kafka ──────────────────────────────────────────────────
  log('KAFKA', 'A aguardar propagação de todos os jogos via Kafka...');
  const t4 = Date.now();
  const propagados = await aguardarTodosJogos(codigos, users[0].token);
  log('KAFKA', `${propagados}/${jogosOk} propagados em ${Date.now() - t4}ms`);

  // ── Fase 5: BET STORM sustentado ────────────────────────────────────────────
  const tipos = ['1', 'X', '2'];
  let betOk = 0, betFail = 0;
  const telemetria = [];   // apostas por janela de 1s
  let janela = 0;
  const betStart = Date.now();

  // Gerar todas as tarefas: user_i aposta no game_j
  const tasks = [];
  for (let ui = 0; ui < users.length; ui++) {
    const u = users[ui];
    for (let gi = 0; gi < codigos.length; gi++) {
      const cod   = codigos[gi];
      const tipo  = tipos[(ui + gi) % 3];
      tasks.push(() => fetchJSON(`${APOSTAS_URL}/api/apostas`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${u.token}` },
        body: JSON.stringify({ codigoJogo: cod, tipoAposta: tipo, montanteApostado: MONTANTE }),
      }));
    }
  }

  log('BET', c('1;33', `BET STORM INICIADO — ${tasks.length} apostas, concorrência ${CONCURRENCY}`));
  console.log('');

  // Ticker de 1s para imprimir throughput em tempo real
  let lastCount = 0;
  const ticker = setInterval(() => {
    janela++;
    const thisWindow = betOk - lastCount;
    telemetria.push(thisWindow);
    lastCount = betOk;
    const bar = '█'.repeat(Math.min(50, Math.floor(thisWindow / 20)));
    const col  = thisWindow >= 1000 ? '32' : thisWindow >= 700 ? '33' : '31';
    process.stdout.write(
      `\r  [s${String(janela).padStart(2,'0')}] ${c(col, String(thisWindow).padStart(5))} ap/s  ${c(col, bar.padEnd(50))}`
    );
    if (janela >= BETTING_SECS) {
      clearInterval(ticker);
      process.stdout.write('\n');
    }
  }, 1000);

  // Correr o pool — não espera 30s, corre até esgotar as tasks
  await concurrentPool(tasks, CONCURRENCY, r => {
    if (r?.status === 201) betOk++;
    else betFail++;
  });

  clearInterval(ticker);
  process.stdout.write('\n');

  const betSecs = (Date.now() - betStart) / 1000;
  const avgThroughput = Math.round(betOk / betSecs);

  log('BET', c('1;33', `${betOk} apostas em ${betSecs.toFixed(1)}s — média ${avgThroughput}/s — falhas ${betFail}`));
  if (telemetria.length > 0) {
    const peak = Math.max(...telemetria);
    const p50  = [...telemetria].sort((a,b)=>a-b)[Math.floor(telemetria.length/2)];
    log('STATS', `Peak: ${c('1;32', peak + '/s')}  |  Mediana: ${p50}/s  |  Janelas: ${telemetria.length}`);
  }

  // ── Fase 6: Transicionar jogos ───────────────────────────────────────────────
  log('RABBIT', `A mover ${TOTAL_GAMES} jogos → EmCurso em paralelo...`);
  await concurrentPool(
    codigos.map(cod => () => fetchJSON(`${RESULTADOS_URL}/api/jogos/${cod}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json', 'X-API-Key': API_KEY },
      body: JSON.stringify({
        estadoAtual: 2,
        golosCasa: Math.floor(Math.random() * 4),
        golosVisitante: Math.floor(Math.random() * 4),
      }),
    })),
    30
  );

  await sleep(500);

  log('RABBIT', `A finalizar ${TOTAL_GAMES} jogos → RabbitMQ bet-resolution para ~${betOk} apostas...`);
  const finStart = Date.now();
  await concurrentPool(
    codigos.map(cod => () => fetchJSON(`${RESULTADOS_URL}/api/jogos/${cod}`, {
      method: 'PATCH',
      headers: { 'Content-Type': 'application/json', 'X-API-Key': API_KEY },
      body: JSON.stringify({ estadoAtual: 3 }),
    })),
    30
  );
  log('RABBIT', `${TOTAL_GAMES} mensagens enfileiradas em ${Date.now() - finStart}ms`);

  // ── Fase 7: Aguardar resolução ───────────────────────────────────────────────
  log('INFO', `A aguardar resolução RabbitMQ de ~${betOk} apostas...`);
  await sleep(15000); // dar tempo ao RabbitMQ + trigger SQL para processar tudo

  // ── Analytics ────────────────────────────────────────────────────────────────
  const [resumo, alertasRes] = await Promise.all([
    fetchJSON(`${ANALYTICS_URL}/api/analytics/resumo`),
    fetchJSON(`${ANALYTICS_URL}/api/analytics/alertas`),
  ]);

  const total = (Date.now() - start) / 1000;
  const d  = resumo.status === 200 ? resumo.body : {};
  const tv = d.taxasVitoria ?? { ganhou1: 0, ganhouX: 0, ganhou2: 0, total: 0 };
  const alertas = alertasRes.status === 200 ? alertasRes.body : [];
  const exps = Object.entries(d.exposicaoFinanceira ?? {});
  const porTipo = alertas.reduce((acc, a) => { acc[a.tipo] = (acc[a.tipo] || 0) + 1; return acc; }, {});

  console.log(`\n${c('1;36', '══════════════════════════════════════════════════')}`);
  console.log(c('1', ' RESULTADO FINAL'));
  console.log(`${c('36', '──────────────────────────────────────────────────')}`);
  console.log(` Duração total          : ${c('1', total.toFixed(1) + 's')}`);
  console.log(` Apostas colocadas      : ${c('1;33', betOk)}  (falhas: ${betFail})`);
  console.log(` Throughput médio       : ${c('1;33', avgThroughput + '/s')}`);
  console.log(` Peak (melhor janela 1s): ${c('1;32', (Math.max(...(telemetria.length ? telemetria : [0])) + '/s'))}`);
  console.log(` Taxas vitória (1/X/2)  : ${c('32', tv.ganhou1)}/${c('33', tv.ganhouX)}/${c('36', tv.ganhou2)}  (total analytics: ${tv.total})`);
  console.log(` Volume apostado        : ${c('1', ((betOk * MONTANTE).toFixed(0)) + ' €')}`);
  console.log(` Apostas/min (analytics): ${c('1', d.apostas?.ultimos5Minutos ?? 0)}`);
  console.log(` Utilizadores ativos    : ${d.utilizadoresAtivos5Min ?? 0}`);
  console.log(` Jogos com exposição    : ${exps.length}  (acima 500€: ${exps.filter(([,v]) => Number(v) > 500).length})`);
  console.log(` Alertas                : ${c('1', alertas.length)}  ${Object.entries(porTipo).map(([t,n]) => `[${t}]×${n}`).join('  ')}`);
  console.log(`${c('1;36', '══════════════════════════════════════════════════')}\n`);
}

main().catch(e => { console.error(c('91', 'Erro fatal:'), e); process.exit(1); });
