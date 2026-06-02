/**
 * BetStrike Parte 2 — Stress Test
 *
 * Fluxo por jogo:
 *   1. POST /api/jogos (ApiResultados) → publica game-events em Kafka
 *   2. Aguarda jogo aparecer em ApiApostas (Kafka consumer)
 *   3. N utilizadores apostam em paralelo → publica bet-events em Kafka
 *   4. PATCH EmCurso + golos → Kafka
 *   5. PATCH Finalizado → RabbitMQ bet-resolution → resolve N apostas → trigger SQL
 *
 * Configuração (variáveis no topo do ficheiro):
 *   JOGOS          — número de jogos a criar em paralelo
 *   APOSTAS_POR_JOGO — apostas por jogo (utilizadores distintos)
 *   DELAY_ENTRE_FASES — ms entre EmCurso e Finalizado
 */

import { createRequire } from 'module';
const require = createRequire(import.meta.url);

// ── Configuração ────────────────────────────────────────────────────────────
const RESULTADOS_URL  = 'http://localhost:5204';
const APOSTAS_URL     = 'http://localhost:5141';
const ANALYTICS_URL   = 'http://localhost:5400';
const API_KEY         = 'Ra1MTLOcHOaJL8EXlAaKQ8PjGMrccSgr5w4yxg0c6QNRb1zP0WVAj81KcX6uQV0T';

const JOGOS             = 5;   // jogos criados em paralelo
const APOSTAS_POR_JOGO  = 8;   // utilizadores a apostar por jogo
const DELAY_ENTRE_FASES = 2000; // ms entre EmCurso e Finalizado

// ── Prefixo de jogo único por run ──────────────────────────────────────────
const RUN_ID = Date.now().toString().slice(-4); // últimos 4 dígitos do timestamp

// ── Estatísticas globais ───────────────────────────────────────────────────
const stats = {
  jogos:      { criados: 0, falhas: 0 },
  kafka:      { propagados: 0, timeout: 0 },
  apostas:    { colocadas: 0, falhas: 0 },
  rabbitmq:   { resolvidas: 0, anuladas: 0, falhas: 0 },
  latencias:  [],
};

const timings = {};
const start = Date.now();

// ── Helpers ────────────────────────────────────────────────────────────────
function log(tag, msg) {
  const t = ((Date.now() - start) / 1000).toFixed(1);
  console.log(`[${String(t).padStart(6)}s] [${tag}] ${msg}`);
}

async function fetchJSON(url, opts = {}) {
  const r = await fetch(url, opts);
  const text = await r.text();
  let body;
  try { body = JSON.parse(text); } catch { body = text; }
  return { status: r.status, body };
}

async function registar(email) {
  const r = await fetchJSON(`${APOSTAS_URL}/api/auth/registar`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: 'Stress123!', nomeCompleto: 'Stress User' }),
  });
  if (r.status === 200) return r.body.token;
  // já existe → login
  const l = await fetchJSON(`${APOSTAS_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: 'Stress123!' }),
  });
  if (l.status === 200) return l.body.token;
  throw new Error(`auth falhou para ${email}: ${l.status}`);
}

async function aguardarJogoEmApostas(codigoJogo, token, tentativas = 10) {
  for (let i = 0; i < tentativas; i++) {
    const r = await fetchJSON(`${APOSTAS_URL}/api/jogos/${codigoJogo}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (r.status === 200) return r.body;
    await sleep(500);
  }
  return null;
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

function codigoJogo(idx) {
  // FUT-AAAA-RRNN: RR = run_id (2 chars), NN = índice (2 chars)
  // Limitado a 4 dígitos após o hífen final — usar sufixo numérico
  const nn = String(idx + 1).padStart(2, '0');
  const rr = RUN_ID.slice(-2);
  return `FUT-2026-${rr}${nn}`;
}

// ── Fluxo por jogo ─────────────────────────────────────────────────────────
async function fluxoJogo(idx, tokens) {
  const codigo = codigoJogo(idx);
  const equipas = [
    ['Benfica','Porto'], ['Sporting','Braga'], ['Guimaraes','Famalicao'],
    ['Vitoria','Arouca'], ['Boavista','Estoril'],
  ][idx % 5];

  const jogoStart = Date.now();

  // 1. Criar jogo
  const criar = await fetchJSON(`${RESULTADOS_URL}/api/jogos`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-API-Key': API_KEY },
    body: JSON.stringify({
      codigoJogo: codigo,
      equipaCasa: equipas[0],
      equipaFora: equipas[1],
      tipoCompeticao: 'Primeira Liga',
      dataHoraInicio: '2026-12-20T20:00:00Z',
      estadoAtual: 1,
    }),
  });

  if (criar.status !== 201) {
    stats.jogos.falhas++;
    log('ERRO', `Criar jogo ${codigo}: HTTP ${criar.status} — ${JSON.stringify(criar.body)}`);
    return;
  }
  stats.jogos.criados++;
  log('JOGO', `${codigo} criado → Kafka a publicar game-events`);

  // 2. Aguardar Kafka propagar
  const jogoApostas = await aguardarJogoEmApostas(codigo, tokens[0]);
  if (!jogoApostas) {
    stats.kafka.timeout++;
    log('WARN', `${codigo} não chegou a ApiApostas em 5s`);
    return;
  }
  stats.kafka.propagados++;
  log('KAFKA', `${codigo} propagado — Odd1:${jogoApostas.Odd1} OddX:${jogoApostas.OddX} Odd2:${jogoApostas.Odd2}`);

  // 3. Apostas em paralelo (N utilizadores)
  const tipos = ['1', 'X', '2'];
  const apostasOk = await Promise.allSettled(
    tokens.map(async (token, i) => {
      const r = await fetchJSON(`${APOSTAS_URL}/api/apostas`, {
        method: 'POST',
        headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
        body: JSON.stringify({
          codigoJogo: codigo,
          tipoAposta: tipos[i % 3],
          montanteApostado: 5, // fixo para não esgotar saldo com jogos paralelos
        }),
      });
      if (r.status === 201) {
        stats.apostas.colocadas++;
        return r.body.id;
      } else {
        stats.apostas.falhas++;
        log('WARN', `Aposta falhou em ${codigo} user${i}: HTTP ${r.status}`);
        return null;
      }
    })
  );
  const apostasIds = apostasOk.filter(r => r.status === 'fulfilled' && r.value).map(r => r.value);
  log('APOSTA', `${codigo} — ${apostasIds.length}/${tokens.length} apostas colocadas`);

  // 4. EmCurso + golos
  const golosCasa = Math.floor(Math.random() * 4);
  const golosVisitante = Math.floor(Math.random() * 4);

  const emCurso = await fetchJSON(`${RESULTADOS_URL}/api/jogos/${codigo}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json', 'X-API-Key': API_KEY },
    body: JSON.stringify({ estadoAtual: 2, golosCasa, golosVisitante }),
  });
  if (emCurso.status !== 200) {
    log('WARN', `${codigo} EmCurso falhou: ${emCurso.status}`);
  } else {
    log('ESTADO', `${codigo} → EmCurso (${golosCasa}-${golosVisitante})`);
  }

  await sleep(DELAY_ENTRE_FASES);

  // 5. Finalizado → RabbitMQ
  const finalizado = await fetchJSON(`${RESULTADOS_URL}/api/jogos/${codigo}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json', 'X-API-Key': API_KEY },
    body: JSON.stringify({ estadoAtual: 3 }),
  });
  if (finalizado.status !== 200) {
    stats.rabbitmq.falhas++;
    log('ERRO', `${codigo} Finalizado falhou: ${finalizado.status}`);
    return;
  }
  log('RABBIT', `${codigo} → Finalizado — mensagem bet-resolution publicada em RabbitMQ`);

  // 6. Aguardar resolução e verificar
  await sleep(3000);

  // verificar resolução em todos os utilizadores que apostaram
  const todasApostas = await Promise.all(
    tokens.map(t => fetchJSON(`${APOSTAS_URL}/api/apostas`, {
      headers: { Authorization: `Bearer ${t}` },
    }))
  );

  const todasIds = new Set(apostasIds);
  const resolvidas = todasApostas
    .filter(r => r.status === 200)
    .flatMap(r => r.body)
    .filter(a => todasIds.has(a.Id) && (a.Estado === 2 || a.Estado === 3 || a.Estado === 4));

  const ganhas   = resolvidas.filter(a => a.Estado === 2).length;
  const perdidas = resolvidas.filter(a => a.Estado === 3).length;
  const anuladas = resolvidas.filter(a => a.Estado === 4).length;
  stats.rabbitmq.resolvidas += ganhas + perdidas;
  stats.rabbitmq.anuladas   += anuladas;

  const resultado = golosCasa > golosVisitante ? '1 ganha' : golosCasa === golosVisitante ? 'X empate' : '2 ganha';
  log('RESOLV', `${codigo} (${golosCasa}-${golosVisitante}, ${resultado}) — ganhas:${ganhas} perdidas:${perdidas} anuladas:${anuladas} | pendentes:${apostasIds.length - resolvidas.length}`);

  timings[codigo] = Date.now() - jogoStart;
}

// ── Analytics final ────────────────────────────────────────────────────────
async function printAnalytics() {
  const r = await fetchJSON(`${ANALYTICS_URL}/api/analytics/resumo`);
  if (r.status !== 200) { log('WARN', 'Analytics não respondeu'); return; }
  const d = r.body;
  const tv = d.taxasVitoria;
  console.log('\n══════════════════════════════════════════');
  console.log(' ANALYTICS (estado final)');
  console.log('══════════════════════════════════════════');
  console.log(` Apostas/min          : ${d.apostas.ultimoMinuto}`);
  console.log(` Apostas/5min         : ${d.apostas.ultimos5Minutos}`);
  console.log(` Utilizadores ativos  : ${d.utilizadoresAtivos5Min}`);
  console.log(` Taxas vitória  1/X/2 : ${tv.ganhou1}/${tv.ganhouX}/${tv.ganhou2} (total: ${tv.total})`);
  console.log(' Volume por competição:');
  for (const [k, v] of Object.entries(d.volumePorCompeticao ?? {})) {
    console.log(`   ${k}: ${Number(v).toFixed(2)} €`);
  }
  console.log(' Exposição por jogo:');
  for (const [k, v] of Object.entries(d.exposicaoFinanceira ?? {})) {
    console.log(`   ${k}: ${Number(v).toFixed(2)} €`);
  }

  const alertas = await fetchJSON(`${ANALYTICS_URL}/api/analytics/alertas`);
  if (alertas.status === 200 && alertas.body.length > 0) {
    console.log(` Alertas (${alertas.body.length}):`);
    alertas.body.forEach(a => console.log(`   [${a.tipo}] ${a.mensagem}`));
  } else {
    console.log(' Alertas: nenhum');
  }
  console.log('══════════════════════════════════════════\n');
}

// ── Main ───────────────────────────────────────────────────────────────────
async function main() {
  console.log('══════════════════════════════════════════');
  console.log(` BetStrike Stress Test — run ${RUN_ID}`);
  console.log(` ${JOGOS} jogos × ${APOSTAS_POR_JOGO} apostas = ${JOGOS * APOSTAS_POR_JOGO} apostas totais`);
  console.log('══════════════════════════════════════════\n');

  // Criar/login utilizadores de stress
  log('AUTH', `A registar/login ${APOSTAS_POR_JOGO} utilizadores...`);
  const tokens = await Promise.all(
    Array.from({ length: APOSTAS_POR_JOGO }, (_, i) =>
      registar(`stress${RUN_ID}u${i}@betstrike.pt`)
    )
  );
  log('AUTH', `${tokens.length} tokens prontos`);

  // Lançar todos os jogos em paralelo
  log('START', `A lançar ${JOGOS} jogos em paralelo...`);
  await Promise.allSettled(
    Array.from({ length: JOGOS }, (_, i) => fluxoJogo(i, tokens))
  );

  // Aguardar resolução final
  log('INFO', 'Aguardar 3s para RabbitMQ terminar...');
  await sleep(3000);

  // Sumário
  const total = (Date.now() - start) / 1000;
  console.log('\n══════════════════════════════════════════');
  console.log(' SUMÁRIO DO STRESS TEST');
  console.log('══════════════════════════════════════════');
  console.log(` Duração total        : ${total.toFixed(1)}s`);
  console.log(` Jogos criados        : ${stats.jogos.criados} / ${JOGOS} (falhas: ${stats.jogos.falhas})`);
  console.log(` Kafka propagados     : ${stats.kafka.propagados} / ${stats.jogos.criados} (timeout: ${stats.kafka.timeout})`);
  console.log(` Apostas colocadas    : ${stats.apostas.colocadas} (falhas: ${stats.apostas.falhas})`);
  console.log(` Apostas resolvidas   : ${stats.rabbitmq.resolvidas} (anuladas: ${stats.rabbitmq.anuladas}, falhas: ${stats.rabbitmq.falhas})`);
  if (Object.keys(timings).length > 0) {
    const lats = Object.values(timings);
    const avg = lats.reduce((a, b) => a + b, 0) / lats.length;
    const max = Math.max(...lats);
    const min = Math.min(...lats);
    console.log(` Latência por jogo    : min ${min}ms  avg ${avg.toFixed(0)}ms  max ${max}ms`);
  }
  console.log('══════════════════════════════════════════\n');

  await printAnalytics();
}

main().catch(e => { console.error('Erro fatal:', e); process.exit(1); });
