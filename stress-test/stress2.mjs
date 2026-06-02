/**
 * BetStrike Stress Test v2 — Carga máxima com alertas de exposição
 *
 * O que acontece em simultâneo:
 *   • 10 jogos normais (8 users × 5€)
 *   •  1 jogo "high-stakes" (20 users × 40€) → exposição >500€ → alerta exposicao-elevada
 *   •  1 jogo cancelado → todas as apostas anuladas + reembolsos
 *   • Analytics a acumular tudo em tempo real
 *
 * Fases sobrepostas:
 *   t=0s   criar 12 jogos em paralelo + top-up saldo dos users high-stakes
 *   t~0.5s todos os jogos propagados via Kafka → apostas em paralelo
 *   t~1s   EmCurso em todos os jogos + cancelamento do jogo especial
 *   t~3s   Finalizado em todos os jogos normais → RabbitMQ resolve ~90 apostas
 *   t~6s   verificação + sumário Analytics
 */

// ── Configuração ─────────────────────────────────────────────────────────────
const RESULTADOS_URL = 'http://localhost:5204';
const APOSTAS_URL    = 'http://localhost:5141';
const ANALYTICS_URL  = 'http://localhost:5400';
const API_KEY        = 'Ra1MTLOcHOaJL8EXlAaKQ8PjGMrccSgr5w4yxg0c6QNRb1zP0WVAj81KcX6uQV0T';
const ADMIN_EMAIL    = 'admin@betstrike.pt';
const ADMIN_PASSWORD = 'Admin123!';

const JOGOS_NORMAIS      = 10;   // jogos com carga normal
const USERS_POR_JOGO     = 8;    // utilizadores por jogo normal
const MONTANTE_NORMAL    = 5;    // € por aposta normal

const USERS_HIGH_STAKES  = 20;   // utilizadores no jogo high-stakes
const MONTANTE_HIGH      = 40;   // € por aposta high-stakes (exposição = 40×2.1×20 = 1680€)

const DELAY_EMCURSO_FINAL = 2000; // ms entre EmCurso e Finalizado

// ── Estado global ─────────────────────────────────────────────────────────────
const RUN_ID = Date.now().toString().slice(-4);
const start  = Date.now();
const stats  = {
  jogos:    { criados: 0, falhas: 0 },
  kafka:    { propagados: 0, timeout: 0 },
  apostas:  { colocadas: 0, falhas: 0 },
  rabbit:   { resolvidas: 0, anuladas: 0 },
  topups:   0,
};

// ── Helpers ───────────────────────────────────────────────────────────────────
const elapsed = () => `${((Date.now() - start) / 1000).toFixed(1)}s`.padStart(7);

function log(tag, msg) {
  const colors = { JOGO:'36', KAFKA:'32', APOSTA:'33', RABBIT:'35', RESOLV:'32', CANCEL:'31', HS:'95', AUTH:'90', INFO:'37', ERR:'91', ANALYTICS:'34' };
  const c = colors[tag] || '37';
  console.log(`[${elapsed()}] \x1b[${c}m[${tag.padEnd(8)}]\x1b[0m ${msg}`);
}

async function fetchJSON(url, opts = {}) {
  const r = await fetch(url, opts);
  const text = await r.text();
  let body; try { body = JSON.parse(text); } catch { body = text; }
  return { status: r.status, body };
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

// Todos os códigos: FUT-2026-XXYY onde XX = primeiros 2 dígitos do RUN_ID, YY = índice 2 dígitos
const BASE = RUN_ID.slice(0, 2);
function codigoNormal(idx)    { return `FUT-2026-${BASE}${String(idx + 1).padStart(2, '0')}`; }
const CODIGO_HS     = `FUT-2026-${BASE}88`;
const CODIGO_CANCEL = `FUT-2026-${BASE}99`;

const EQUIPAS = [
  ['Benfica','Porto'], ['Sporting','Braga'], ['Vitoria','Guimaraes'],
  ['Famalicao','Boavista'], ['Estoril','Arouca'], ['Moreirense','Vizela'],
  ['Casa Pia','Gil Vicente'], ['Farense','Portimonense'], ['Estrela','Nacional'],
  ['Penafiel','AVS'],
];

// ── Auth ──────────────────────────────────────────────────────────────────────
async function getToken(email, password = 'Stress123!', nome = 'Stress User') {
  let r = await fetchJSON(`${APOSTAS_URL}/api/auth/registar`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password, nomeCompleto: nome }),
  });
  if (r.status === 200) return { token: r.body.token, id: r.body.utilizadorId, novo: true };
  r = await fetchJSON(`${APOSTAS_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  if (r.status === 200) return { token: r.body.token, id: r.body.utilizadorId, novo: false };
  throw new Error(`auth falhou ${email}: ${r.status}`);
}

async function topUp(adminToken, userId, valor) {
  const r = await fetchJSON(`${APOSTAS_URL}/api/admin/utilizadores/${userId}/saldo`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` },
    body: JSON.stringify({ valor, motivo: `stress2-topup-${RUN_ID}` }),
  });
  if (r.status === 200) { stats.topups++; return r.body.SaldoAtual; }
  return null;
}

async function aguardarJogo(codigoJogo, token, tentativas = 12) {
  for (let i = 0; i < tentativas; i++) {
    const r = await fetchJSON(`${APOSTAS_URL}/api/jogos/${codigoJogo}`, {
      headers: { Authorization: `Bearer ${token}` },
    });
    if (r.status === 200) return r.body;
    await sleep(400);
  }
  return null;
}

async function criarJogo(cod, equipas, estado = 1) {
  const r = await fetchJSON(`${RESULTADOS_URL}/api/jogos`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-API-Key': API_KEY },
    body: JSON.stringify({
      codigoJogo: cod, equipaCasa: equipas[0], equipaFora: equipas[1],
      tipoCompeticao: 'Primeira Liga', dataHoraInicio: '2026-12-20T20:00:00Z', estadoAtual: estado,
    }),
  });
  return r;
}

async function patchJogo(cod, body) {
  return fetchJSON(`${RESULTADOS_URL}/api/jogos/${cod}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json', 'X-API-Key': API_KEY },
    body: JSON.stringify(body),
  });
}

async function apostar(token, codigoJogo, tipoAposta, montante) {
  return fetchJSON(`${APOSTAS_URL}/api/apostas`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ codigoJogo, tipoAposta, montanteApostado: montante }),
  });
}

// ── Jogo normal ───────────────────────────────────────────────────────────────
async function fluxoNormal(idx, tokens) {
  const cod     = codigoNormal(idx);
  const equipas = EQUIPAS[idx % EQUIPAS.length];
  const tipos   = ['1', 'X', '2'];

  // Criar
  const cr = await criarJogo(cod, equipas);
  if (cr.status !== 201) { stats.jogos.falhas++; log('ERR', `Criar ${cod}: ${cr.status}`); return; }
  stats.jogos.criados++;

  // Kafka
  const jogo = await aguardarJogo(cod, tokens[0]);
  if (!jogo) { stats.kafka.timeout++; log('ERR', `Kafka timeout ${cod}`); return; }
  stats.kafka.propagados++;
  log('JOGO', `${cod} ${equipas[0]} vs ${equipas[1]} — propagado via Kafka`);

  // Apostas
  const resultados = await Promise.allSettled(
    tokens.map((t, i) => apostar(t, cod, tipos[i % 3], MONTANTE_NORMAL))
  );
  const ids = resultados.filter(r => r.status === 'fulfilled' && r.value.status === 201)
                        .map(r => r.value.body.id);
  stats.apostas.colocadas += ids.length;
  stats.apostas.falhas    += tokens.length - ids.length;
  log('APOSTA', `${cod} — ${ids.length}/${tokens.length} apostas OK`);

  // EmCurso + golos
  const g1 = Math.floor(Math.random() * 4), g2 = Math.floor(Math.random() * 4);
  await patchJogo(cod, { estadoAtual: 2, golosCasa: g1, golosVisitante: g2 });

  await sleep(DELAY_EMCURSO_FINAL);

  // Finalizado → RabbitMQ
  const fin = await patchJogo(cod, { estadoAtual: 3 });
  if (fin.status !== 200) { stats.rabbit.falhas++; return; }
  log('RABBIT', `${cod} → Finalizado (${g1}-${g2}) — bet-resolution enviado`);

  // Verificar resolução
  await sleep(3500);
  const checks = await Promise.all(tokens.map(t =>
    fetchJSON(`${APOSTAS_URL}/api/apostas`, { headers: { Authorization: `Bearer ${t}` } })
  ));
  const allIds = new Set(ids);
  const resolvidas = checks.flatMap(r => r.status === 200 ? r.body : [])
                           .filter(a => allIds.has(a.Id) && a.Estado !== 1);
  const ganhas = resolvidas.filter(a => a.Estado === 2).length;
  const perdidas = resolvidas.filter(a => a.Estado === 3).length;
  stats.rabbit.resolvidas += ganhas + perdidas;

  const res = g1 > g2 ? '1' : g1 === g2 ? 'X' : '2';
  log('RESOLV', `${cod} (${g1}-${g2}, ${res}) ganhas:${ganhas} perdidas:${perdidas} pendentes:${ids.length - resolvidas.length}`);
}

// ── Jogo high-stakes (trigger exposição) ─────────────────────────────────────
async function fluxoHighStakes(adminToken, tokens) {
  const codHS = CODIGO_HS;
  log('HS', `Jogo high-stakes: ${codHS} — top-up ${tokens.length} users a 200€...`);

  // Top-up todos os utilizadores high-stakes
  await Promise.all(tokens.map(({ token, id }) => topUp(adminToken, id, 200)));
  log('HS', `Top-up concluído (${tokens.length} users × 200€). A criar jogo...`);

  const cr = await criarJogo(codHS, ['Benfica', 'Sporting']);
  if (cr.status !== 201) { log('ERR', `High-stakes criar falhou: ${cr.status} ${JSON.stringify(cr.body)}`); return; }
  stats.jogos.criados++;

  const jogo = await aguardarJogo(codHS, tokens[0].token);
  if (!jogo) { stats.kafka.timeout++; log('ERR', 'High-stakes Kafka timeout'); return; }
  stats.kafka.propagados++;
  log('HS', `${codHS} propagado via Kafka — Odd1:${jogo.Odd1}`);

  // Apostas massivas: todos apostam no '1'
  const resultados = await Promise.allSettled(
    tokens.map(({ token }) => apostar(token, codHS, '1', MONTANTE_HIGH))
  );
  const ids = resultados.filter(r => r.status === 'fulfilled' && r.value.status === 201)
                        .map(r => r.value.body.id);
  const exposicaoEstimada = (ids.length * MONTANTE_HIGH * jogo.Odd1).toFixed(0);
  stats.apostas.colocadas += ids.length;
  log('HS', `${codHS} — ${ids.length}/${tokens.length} apostas × €${MONTANTE_HIGH} — exposição estimada: €${exposicaoEstimada}`);

  // Verificar alerta de exposição no Analytics (pode já ter sido gerado)
  await sleep(500);
  const alertas = await fetchJSON(`${ANALYTICS_URL}/api/analytics/alertas`);
  const alertaExp = alertas.status === 200
    ? alertas.body.filter(a => a.tipo === 'exposicao-elevada' && a.mensagem?.includes(codHS))
    : [];
  if (alertaExp.length > 0) {
    log('HS', `\x1b[1m\x1b[91m ALERTA EXPOSIÇÃO DETECTADO: ${alertaExp[0].mensagem}\x1b[0m`);
  }

  // EmCurso e Finalizado
  await patchJogo(codHS, { estadoAtual: 2, golosCasa: 2, golosVisitante: 1 });
  await sleep(DELAY_EMCURSO_FINAL);
  await patchJogo(codHS, { estadoAtual: 3 });
  log('HS', `${codHS} → Finalizado (2-1) — RabbitMQ a resolver ${ids.length} apostas ganhas`);

  await sleep(3500);
  const checks = await Promise.all(tokens.map(({ token }) =>
    fetchJSON(`${APOSTAS_URL}/api/apostas`, { headers: { Authorization: `Bearer ${token}` } })
  ));
  const allIds = new Set(ids);
  const resolvidas = checks.flatMap(r => r.status === 200 ? r.body : [])
                           .filter(a => allIds.has(a.Id) && a.Estado !== 1);
  const ganhas = resolvidas.filter(a => a.Estado === 2).length;
  stats.rabbit.resolvidas += ganhas;
  log('HS', `${codHS} — ${ganhas}/${ids.length} apostas ganhas resolvidas`);
}

// ── Jogo cancelado (trigger anuladas) ────────────────────────────────────────
async function fluxoCancelamento(tokens) {
  const codC = CODIGO_CANCEL;

  const cr = await criarJogo(codC, ['Porto', 'Braga']);
  if (cr.status !== 201) { log('ERR', `Cancel criar: ${cr.status}`); return; }
  stats.jogos.criados++;

  const jogo = await aguardarJogo(codC, tokens[0]);
  if (!jogo) { stats.kafka.timeout++; return; }
  stats.kafka.propagados++;

  // Apostas
  const resultados = await Promise.allSettled(
    tokens.slice(0, 5).map((t, i) => apostar(t, codC, ['1','X','2'][i % 3], MONTANTE_NORMAL))
  );
  const ids = resultados.filter(r => r.status === 'fulfilled' && r.value.status === 201)
                        .map(r => r.value.body.id);
  stats.apostas.colocadas += ids.length;
  log('CANCEL', `${codC} — ${ids.length} apostas colocadas — a cancelar jogo...`);

  // Cancelar imediatamente (sem passar por EmCurso)
  const can = await patchJogo(codC, { estadoAtual: 4 });
  if (can.status !== 200) { log('ERR', `Cancelar ${codC}: ${can.status}`); return; }
  log('CANCEL', `${codC} → Cancelado — RabbitMQ a anular ${ids.length} apostas + reembolsos`);

  await sleep(3500);
  const checks = await Promise.all(tokens.slice(0, 5).map(t =>
    fetchJSON(`${APOSTAS_URL}/api/apostas`, { headers: { Authorization: `Bearer ${t}` } })
  ));
  const allIds = new Set(ids);
  const anuladas = checks.flatMap(r => r.status === 200 ? r.body : [])
                         .filter(a => allIds.has(a.Id) && a.Estado === 4).length;
  stats.rabbit.anuladas += anuladas;
  log('CANCEL', `${codC} — ${anuladas}/${ids.length} apostas anuladas com reembolso`);
}

// ── Analytics snapshot ────────────────────────────────────────────────────────
async function printAnalytics() {
  const [resumo, alertas] = await Promise.all([
    fetchJSON(`${ANALYTICS_URL}/api/analytics/resumo`),
    fetchJSON(`${ANALYTICS_URL}/api/analytics/alertas`),
  ]);

  if (resumo.status !== 200) { log('ERR', 'Analytics não respondeu'); return; }
  const d  = resumo.body;
  const tv = d.taxasVitoria;

  console.log('\n\x1b[34m══════════════════════════════════════════════════\x1b[0m');
  console.log('\x1b[1m ANALYTICS — SNAPSHOT FINAL\x1b[0m');
  console.log('\x1b[34m══════════════════════════════════════════════════\x1b[0m');
  console.log(` Apostas/min            : ${d.apostas.ultimoMinuto}`);
  console.log(` Apostas/5min           : ${d.apostas.ultimos5Minutos}`);
  console.log(` Utilizadores ativos    : ${d.utilizadoresAtivos5Min}`);
  console.log(` Taxas vitória (1/X/2)  : ${tv.ganhou1}/${tv.ganhouX}/${tv.ganhou2}  (total resolvidas: ${tv.total})`);

  const vols = Object.entries(d.volumePorCompeticao ?? {});
  if (vols.length) {
    console.log(' Volume por competição  :');
    vols.forEach(([k, v]) => console.log(`   ${k || '(sem tipo)'}: ${Number(v).toFixed(2)} €`));
  }

  const exps = Object.entries(d.exposicaoFinanceira ?? {});
  if (exps.length) {
    console.log(' Exposição por jogo     :');
    exps.forEach(([k, v]) => {
      const warn = Number(v) > 500 ? ' \x1b[91m⚠ ACIMA LIMIAR\x1b[0m' : '';
      console.log(`   ${k}: ${Number(v).toFixed(2)} €${warn}`);
    });
  } else {
    console.log(' Exposição por jogo     : (jogos já finalizados — exposição limpa)');
  }

  if (alertas.status === 200 && alertas.body.length > 0) {
    const porTipo = alertas.body.reduce((acc, a) => {
      acc[a.tipo] = (acc[a.tipo] || 0) + 1;
      return acc;
    }, {});
    console.log(` Alertas (${alertas.body.length} total):`);
    Object.entries(porTipo).forEach(([tipo, n]) => console.log(`   [${tipo}] × ${n}`));
    const expAlerts = alertas.body.filter(a => a.tipo === 'exposicao-elevada');
    if (expAlerts.length) {
      console.log('\x1b[91m Alertas de exposição:\x1b[0m');
      expAlerts.slice(0, 3).forEach(a => console.log(`   ${a.mensagem}`));
      if (expAlerts.length > 3) console.log(`   ... e mais ${expAlerts.length - 3}`);
    }
  } else {
    console.log(' Alertas: nenhum');
  }
  console.log('\x1b[34m══════════════════════════════════════════════════\x1b[0m\n');
}

// ── Main ──────────────────────────────────────────────────────────────────────
async function main() {
  const totalJogos   = JOGOS_NORMAIS + 2; // +high-stakes +cancelamento
  const totalApostas = JOGOS_NORMAIS * USERS_POR_JOGO + USERS_HIGH_STAKES + 5;
  console.log('\x1b[1m\x1b[36m══════════════════════════════════════════════════\x1b[0m');
  console.log(`\x1b[1m BetStrike Stress Test v2 — run ${RUN_ID}\x1b[0m`);
  console.log(`\x1b[1m ${totalJogos} jogos — ~${totalApostas} apostas — 3 fluxos simultâneos\x1b[0m`);
  console.log('\x1b[1m\x1b[36m══════════════════════════════════════════════════\x1b[0m\n');

  // Auth admin
  log('AUTH', 'Login admin...');
  const adminAuth = await getToken(ADMIN_EMAIL, ADMIN_PASSWORD, 'Admin');
  const adminToken = adminAuth.token;

  // Registar utilizadores normais
  log('AUTH', `A registar ${USERS_POR_JOGO} utilizadores normais...`);
  const normalUsers = await Promise.all(
    Array.from({ length: USERS_POR_JOGO }, (_, i) =>
      getToken(`stress2${RUN_ID}n${i}@betstrike.pt`)
    )
  );
  const normalTokens = normalUsers.map(u => u.token);

  // Registar utilizadores high-stakes
  log('AUTH', `A registar ${USERS_HIGH_STAKES} utilizadores high-stakes...`);
  const hsUsers = await Promise.all(
    Array.from({ length: USERS_HIGH_STAKES }, (_, i) =>
      getToken(`stress2${RUN_ID}hs${i}@betstrike.pt`)
    )
  );
  log('AUTH', `Todos os utilizadores prontos (${normalUsers.length + hsUsers.length} total)`);

  // Lançar tudo em paralelo
  log('INFO', `A lançar ${JOGOS_NORMAIS} jogos normais + high-stakes + cancelamento em simultâneo...`);
  console.log('');

  await Promise.allSettled([
    // 10 jogos normais
    ...Array.from({ length: JOGOS_NORMAIS }, (_, i) => fluxoNormal(i, normalTokens)),
    // 1 jogo high-stakes
    fluxoHighStakes(adminToken, hsUsers),
    // 1 jogo cancelado
    fluxoCancelamento(normalTokens),
  ]);

  log('INFO', 'Aguardar 3s para RabbitMQ terminar processamento residual...');
  await sleep(3000);

  // Sumário
  const total = (Date.now() - start) / 1000;
  console.log('\n\x1b[1m\x1b[36m══════════════════════════════════════════════════\x1b[0m');
  console.log('\x1b[1m SUMÁRIO\x1b[0m');
  console.log('\x1b[1m\x1b[36m══════════════════════════════════════════════════\x1b[0m');
  console.log(` Duração total          : ${total.toFixed(1)}s`);
  console.log(` Jogos criados          : ${stats.jogos.criados} / ${totalJogos}  (falhas: ${stats.jogos.falhas})`);
  console.log(` Kafka propagados       : ${stats.kafka.propagados} / ${stats.jogos.criados}  (timeout: ${stats.kafka.timeout})`);
  console.log(` Top-ups admin          : ${stats.topups} × 200€`);
  console.log(` Apostas colocadas      : ${stats.apostas.colocadas}  (falhas: ${stats.apostas.falhas})`);
  console.log(` Resolvidas (ganhas/perd): ${stats.rabbit.resolvidas}`);
  console.log(` Anuladas (cancelamento): ${stats.rabbit.anuladas}`);
  console.log('\x1b[1m\x1b[36m══════════════════════════════════════════════════\x1b[0m');

  await printAnalytics();
}

main().catch(e => { console.error('\x1b[91mErro fatal:\x1b[0m', e); process.exit(1); });
