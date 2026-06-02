/**
 * BetStrike Stress Test v3 — Full Exhaust
 *
 * ~1000 apostas, 40 utilizadores, 25+ jogos, tudo em paralelo.
 *
 * Fases:
 *   1. Registar/login 40 users + admin em paralelo
 *   2. Top-up de todos os users a 300€ (em batch paralelo)
 *   3. Criar 25 jogos normais + 3 high-stakes + 2 cancel em simultâneo
 *   4. Aguardar propagação Kafka de todos os jogos
 *   5. Bet storm — todos os users a apostar em todos os jogos em simultâneo
 *   6. EmCurso + Finalizado de todos os jogos em simultâneo
 *   7. Aguardar resolução RabbitMQ de ~1000 apostas
 *   8. Relatório final + Analytics
 */

// ── Config ────────────────────────────────────────────────────────────────────
const RESULTADOS_URL = 'http://localhost:5204';
const APOSTAS_URL    = 'http://localhost:5141';
const ANALYTICS_URL  = 'http://localhost:5400';
const API_KEY        = 'Ra1MTLOcHOaJL8EXlAaKQ8PjGMrccSgr5w4yxg0c6QNRb1zP0WVAj81KcX6uQV0T';
const ADMIN_EMAIL    = 'admin@betstrike.pt';
const ADMIN_PASSWORD = 'Admin123!';

const NORMAL_GAMES    = 20;   // jogos normais
const HS_GAMES        = 3;    // jogos high-stakes (>500€ exposição → alertas)
const CANCEL_GAMES    = 2;    // jogos cancelados → apostas anuladas

const NORMAL_USERS    = 40;   // users que apostam nos jogos normais
const HS_USERS        = 25;   // users que apostam nos high-stakes
const CANCEL_USERS    = 15;   // users que apostam nos jogos cancelados

const TOPUP_AMOUNT    = 250;  // top-up adicional por user (50€ inicial + 250€ = 300€ total)
const MONTANTE_NORMAL = 5;    // € por aposta normal
const MONTANTE_HS     = 55;   // € por aposta high-stakes (55×2.1×25 = 2887€ exposição)
const MONTANTE_CANCEL = 5;    // € por aposta cancel

const DELAY_EMCURSO_FINAL = 1500; // ms entre EmCurso e Finalizado

// Ano 2027 para não colidir com runs anteriores
const YEAR = '2027';
const RUN_ID = String(Date.now()).slice(-4);
const BASE   = RUN_ID.slice(0, 2);

// ── Códigos de jogo ────────────────────────────────────────────────────────────
const codigosNormais = Array.from({ length: NORMAL_GAMES }, (_, i) =>
  `FUT-${YEAR}-${BASE}${String(i + 1).padStart(2, '0')}`
);
const codigosHS = Array.from({ length: HS_GAMES }, (_, i) =>
  `FUT-${YEAR}-${BASE}${String(80 + i + 1).padStart(2, '0')}`
);
const codigosCancel = Array.from({ length: CANCEL_GAMES }, (_, i) =>
  `FUT-${YEAR}-${BASE}${String(90 + i + 1).padStart(2, '0')}`
);

const EQUIPAS = [
  ['Benfica','Porto'], ['Sporting','Braga'], ['Vitoria','Guimaraes'],
  ['Famalicao','Boavista'], ['Estoril','Arouca'], ['Moreirense','Vizela'],
  ['Casa Pia','Gil Vicente'], ['Farense','Portimonense'], ['Estrela','Nacional'],
  ['Penafiel','AVS'], ['Benfica','Sporting'], ['Porto','Braga'],
  ['Vitoria','Famalicao'], ['Boavista','Estoril'], ['Arouca','Moreirense'],
  ['Vizela','Casa Pia'], ['Gil Vicente','Farense'], ['Portimonense','Estrela'],
  ['Nacional','Penafiel'], ['AVS','Vitoria'],
];

// ── Estado global ─────────────────────────────────────────────────────────────
const start = Date.now();
const stats = {
  jogos:    { criados: 0, kafkaPropagados: 0, falhas: 0 },
  apostas:  { colocadas: 0, falhas: 0 },
  rabbit:   { ganhas: 0, perdidas: 0, anuladas: 0 },
  topups:   0,
  betsPerSecond: 0,
};
let betStormStart = 0;

const elapsed = () => `${((Date.now() - start) / 1000).toFixed(1)}s`.padStart(7);
const c = (code, s) => `\x1b[${code}m${s}\x1b[0m`;

function log(tag, msg) {
  const colors = {
    SETUP:'90', JOGO:'36', KAFKA:'32', BET:'33', RABBIT:'35',
    RESOLV:'32', HS:'95', CANCEL:'31', INFO:'37', ERR:'91', WARN:'93',
  };
  console.log(`[${elapsed()}] ${c(colors[tag] || '37', `[${tag.padEnd(6)}]`)} ${msg}`);
}

async function fetchJSON(url, opts = {}) {
  const r = await fetch(url, opts);
  const text = await r.text();
  let body; try { body = JSON.parse(text); } catch { body = text; }
  return { status: r.status, body };
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

// ── Auth helpers ──────────────────────────────────────────────────────────────
async function getToken(email, password = 'Exhaust123!', nome = 'Stress User') {
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
  throw new Error(`auth falhou ${email}: ${r.status}`);
}

async function topUpBatch(adminToken, users, valor) {
  const results = await Promise.allSettled(users.map(u =>
    fetchJSON(`${APOSTAS_URL}/api/admin/utilizadores/${u.id}/saldo`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${adminToken}` },
      body: JSON.stringify({ valor, motivo: `stress3-${RUN_ID}` }),
    })
  ));
  const ok = results.filter(r => r.status === 'fulfilled' && r.value.status === 200).length;
  stats.topups += ok;
  return ok;
}

// ── Jogo helpers ──────────────────────────────────────────────────────────────
async function criarJogo(codigo, equipas) {
  const r = await fetchJSON(`${RESULTADOS_URL}/api/jogos`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-API-Key': API_KEY },
    body: JSON.stringify({
      codigoJogo: codigo, equipaCasa: equipas[0], equipaFora: equipas[1],
      tipoCompeticao: 'Primeira Liga', dataHoraInicio: '2027-01-15T20:00:00Z', estadoAtual: 1,
    }),
  });
  if (r.status === 201) { stats.jogos.criados++; return true; }
  stats.jogos.falhas++;
  log('ERR', `Criar ${codigo}: ${r.status} ${JSON.stringify(r.body).slice(0, 80)}`);
  return false;
}

async function aguardarJogos(codigos, token) {
  const pendentes = new Set(codigos);
  for (let tentativa = 0; tentativa < 20 && pendentes.size > 0; tentativa++) {
    await Promise.all([...pendentes].map(async cod => {
      const r = await fetchJSON(`${APOSTAS_URL}/api/jogos/${cod}`, {
        headers: { Authorization: `Bearer ${token}` },
      });
      if (r.status === 200) {
        pendentes.delete(cod);
        stats.jogos.kafkaPropagados++;
      }
    }));
    if (pendentes.size > 0) await sleep(300);
  }
  if (pendentes.size > 0) log('WARN', `${pendentes.size} jogos não propagaram via Kafka`);
  return codigos.length - pendentes.size;
}

async function patchJogo(codigo, body) {
  return fetchJSON(`${RESULTADOS_URL}/api/jogos/${codigo}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json', 'X-API-Key': API_KEY },
    body: JSON.stringify(body),
  });
}

// ── Bet storm ─────────────────────────────────────────────────────────────────
async function betStorm(tokens, codigos, montante, tipoFn) {
  const resultados = await Promise.allSettled(
    tokens.flatMap((u, ui) =>
      codigos.map((cod, ci) =>
        fetchJSON(`${APOSTAS_URL}/api/apostas`, {
          method: 'POST',
          headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${u.token}` },
          body: JSON.stringify({
            codigoJogo: cod,
            tipoAposta: tipoFn(ui, ci),
            montanteApostado: montante,
          }),
        })
      )
    )
  );

  const ok  = resultados.filter(r => r.status === 'fulfilled' && r.value.status === 201);
  const err = resultados.length - ok.length;
  stats.apostas.colocadas += ok.length;
  stats.apostas.falhas    += err;

  const apostaIds = ok.map(r => r.value.body.id).filter(Boolean);
  return apostaIds;
}

// ── Verificar resolução ───────────────────────────────────────────────────────
async function verificarResolucao(tokens, apostaIds) {
  const allIds = new Set(apostaIds);
  const checks = await Promise.all(
    tokens.map(u => fetchJSON(`${APOSTAS_URL}/api/apostas`, {
      headers: { Authorization: `Bearer ${u.token}` },
    }))
  );
  const apostas = checks.flatMap(r => r.status === 200 ? r.body : [])
                        .filter(a => allIds.has(a.Id));
  return {
    ganhas:   apostas.filter(a => a.Estado === 2).length,
    perdidas: apostas.filter(a => a.Estado === 3).length,
    anuladas: apostas.filter(a => a.Estado === 4).length,
    pendentes: apostas.filter(a => a.Estado === 1).length,
  };
}

// ── Analytics final ───────────────────────────────────────────────────────────
async function printAnalytics() {
  const [res, alertasRes] = await Promise.all([
    fetchJSON(`${ANALYTICS_URL}/api/analytics/resumo`),
    fetchJSON(`${ANALYTICS_URL}/api/analytics/alertas`),
  ]);

  if (res.status !== 200) { log('ERR', 'Analytics não respondeu'); return; }
  const d  = res.body;
  const tv = d.taxasVitoria;
  const alertas = alertasRes.status === 200 ? alertasRes.body : [];

  console.log(`\n${c('1;34', '══════════════════════════════════════════════════')}`);
  console.log(c('1', ' ANALYTICS — SNAPSHOT FINAL'));
  console.log(`${c('34', '──────────────────────────────────────────────────')}`);
  console.log(` Apostas/min            : ${c('1', d.apostas.ultimoMinuto)}`);
  console.log(` Apostas/5min           : ${c('1', d.apostas.ultimos5Minutos)}`);
  console.log(` Utilizadores ativos    : ${c('1', d.utilizadoresAtivos5Min)}`);
  console.log(` Taxas vitória (1/X/2)  : ${c('32', tv.ganhou1)}/${c('33', tv.ganhouX)}/${c('36', tv.ganhou2)}  (total resolvidas: ${c('1', tv.total)})`);

  const vols = Object.entries(d.volumePorCompeticao ?? {});
  if (vols.length) {
    console.log(` Volume total apostado  : ${vols.map(([, v]) => Number(v).toFixed(0) + ' €').join(' + ')}`);
  }

  const exps = Object.entries(d.exposicaoFinanceira ?? {})
    .sort(([, a], [, b]) => b - a);
  if (exps.length) {
    const acima = exps.filter(([, v]) => Number(v) > 500);
    console.log(` Exposição por jogo     : ${exps.length} jogos  (${acima.length} acima de 500€)`);
    exps.slice(0, 5).forEach(([k, v]) => {
      const warn = Number(v) > 500 ? c('91', ' ⚠') : '';
      console.log(`   ${k}: ${Number(v).toFixed(2)} €${warn}`);
    });
    if (exps.length > 5) console.log(`   ... e mais ${exps.length - 5} jogos`);
  }

  const porTipo = alertas.reduce((acc, a) => { acc[a.tipo] = (acc[a.tipo] || 0) + 1; return acc; }, {});
  console.log(` Alertas (${alertas.length} total):`);
  Object.entries(porTipo).forEach(([tipo, n]) => {
    const col = tipo.includes('exposicao') ? '91' : '93';
    console.log(`   ${c(col, '[' + tipo + ']')} × ${n}`);
  });
  const expAlerts = alertas.filter(a => a.tipo === 'exposicao-elevada');
  if (expAlerts.length) {
    const max = expAlerts.reduce((m, a) => {
      const v = parseFloat(a.mensagem.match(/[\d.]+(?= €)/)?.[0] || 0);
      return v > m.val ? { val: v, msg: a.mensagem } : m;
    }, { val: 0, msg: '' });
    console.log(` ${c('91', 'Pico de exposição')}: ${max.msg}`);
  }
  console.log(`${c('1;34', '══════════════════════════════════════════════════')}\n`);
}

// ── Main ──────────────────────────────────────────────────────────────────────
async function main() {
  const totalBetsEst = NORMAL_USERS * NORMAL_GAMES + HS_USERS * HS_GAMES + CANCEL_USERS * CANCEL_GAMES;
  const totalJogos   = NORMAL_GAMES + HS_GAMES + CANCEL_GAMES;
  const totalUsers   = Math.max(NORMAL_USERS, HS_USERS, CANCEL_USERS);

  console.log(c('1;36', '══════════════════════════════════════════════════'));
  console.log(c('1',    ` BetStrike Stress Test v3 — FULL EXHAUST — run ${RUN_ID}`));
  console.log(`         ${totalJogos} jogos  ×  ~${totalUsers} users  ≈  ${c('1', '~' + totalBetsEst + ' apostas')}`);
  console.log(`         ${NORMAL_GAMES} normais + ${HS_GAMES} high-stakes + ${CANCEL_GAMES} cancelados`);
  console.log(c('1;36', '══════════════════════════════════════════════════') + '\n');

  // ── FASE 1: Auth ────────────────────────────────────────────────────────────
  log('SETUP', `A registar/login ${NORMAL_USERS} users + admin em paralelo...`);
  const [adminAuth, ...userResults] = await Promise.all([
    getToken(ADMIN_EMAIL, ADMIN_PASSWORD, 'Admin'),
    ...Array.from({ length: NORMAL_USERS }, (_, i) =>
      getToken(`s3${RUN_ID}u${i}@betstrike.pt`)
    ),
  ]);
  const adminToken = adminAuth.token;
  const users = userResults;
  log('SETUP', `${users.length} users prontos`);

  // ── FASE 2: Top-up em batch ─────────────────────────────────────────────────
  log('SETUP', `Top-up de ${users.length} users × ${TOPUP_AMOUNT}€ em paralelo...`);
  const topupOk = await topUpBatch(adminToken, users, TOPUP_AMOUNT);
  log('SETUP', `${topupOk}/${users.length} top-ups OK  → saldo ~${50 + TOPUP_AMOUNT}€/user`);

  const normalUsers = users.slice(0, NORMAL_USERS);
  const hsUsers     = users.slice(0, HS_USERS);
  const cancelUsers = users.slice(0, CANCEL_USERS);

  // ── FASE 3: Criar todos os jogos em simultâneo ──────────────────────────────
  log('JOGO', `A criar ${totalJogos} jogos em simultâneo (${YEAR})...`);
  const criarStart = Date.now();
  await Promise.all([
    ...codigosNormais.map((cod, i) => criarJogo(cod, EQUIPAS[i % EQUIPAS.length])),
    ...codigosHS.map((cod, i) => criarJogo(cod, [['Benfica','Porto'], ['Sporting','Braga'], ['Porto','Sporting']][i])),
    ...codigosCancel.map((cod, i) => criarJogo(cod, [['Guimaraes','Braga'], ['Boavista','Famalicao']][i])),
  ]);
  log('JOGO', `${stats.jogos.criados}/${totalJogos} jogos criados em ${Date.now() - criarStart}ms`);

  // ── FASE 4: Aguardar propagação Kafka ───────────────────────────────────────
  log('KAFKA', `A aguardar propagação de ${totalJogos} jogos via Kafka...`);
  const kafkaStart = Date.now();
  const allCodigos = [...codigosNormais, ...codigosHS, ...codigosCancel];
  const propagados = await aguardarJogos(allCodigos, normalUsers[0].token);
  log('KAFKA', `${propagados}/${stats.jogos.criados} propagados em ${Date.now() - kafkaStart}ms`);

  // ── FASE 5: Bet storm ────────────────────────────────────────────────────────
  const tipos = ['1', 'X', '2'];
  betStormStart = Date.now();

  log('BET', `A lançar BET STORM — ${NORMAL_USERS}×${NORMAL_GAMES} normais + ${HS_USERS}×${HS_GAMES} HS + ${CANCEL_USERS}×${CANCEL_GAMES} cancel...`);

  const [idsNormais, idsHS, idsCancel] = await Promise.all([
    betStorm(normalUsers, codigosNormais, MONTANTE_NORMAL, (ui, ci) => tipos[(ui + ci) % 3]),
    betStorm(hsUsers, codigosHS, MONTANTE_HS, (ui) => tipos[ui % 3]),
    betStorm(cancelUsers, codigosCancel, MONTANTE_CANCEL, (ui) => tipos[ui % 3]),
  ]);

  const betStormMs = Date.now() - betStormStart;
  stats.betsPerSecond = Math.round(stats.apostas.colocadas / (betStormMs / 1000));
  log('BET', `${c('1', stats.apostas.colocadas + ' apostas')} colocadas em ${betStormMs}ms  (${stats.betsPerSecond} apostas/s)  falhas: ${stats.apostas.falhas}`);

  // ── FASE 6: EmCurso + Cancelamento em simultâneo ────────────────────────────
  log('RABBIT', 'A transicionar todos os jogos para EmCurso + Cancelar em simultâneo...');
  await Promise.all([
    ...allCodigos
      .filter(c => !codigosCancel.includes(c))
      .map(cod => patchJogo(cod, {
        estadoAtual: 2,
        golosCasa: Math.floor(Math.random() * 4),
        golosVisitante: Math.floor(Math.random() * 4),
      })),
    ...codigosCancel.map(cod => patchJogo(cod, { estadoAtual: 4 })),
  ]);
  log('RABBIT', `${NORMAL_GAMES + HS_GAMES} jogos → EmCurso  |  ${CANCEL_GAMES} jogos → Cancelado`);

  await sleep(DELAY_EMCURSO_FINAL);

  // ── FASE 7: Finalizado — dispara RabbitMQ para ~1000 apostas ────────────────
  log('RABBIT', `A finalizar ${NORMAL_GAMES + HS_GAMES} jogos → bet-resolution messages em RabbitMQ...`);
  const finStart = Date.now();
  await Promise.all(
    [...codigosNormais, ...codigosHS].map(cod => patchJogo(cod, { estadoAtual: 3 }))
  );
  log('RABBIT', `${NORMAL_GAMES + HS_GAMES} mensagens enfileiradas em ${Date.now() - finStart}ms`);

  // Aguardar resolução
  log('INFO', 'A aguardar resolução RabbitMQ (processar ~1000 apostas via trigger SQL)...');
  await sleep(6000);

  // ── FASE 8: Verificar resultados ─────────────────────────────────────────────
  log('RESOLV', 'A verificar estado final das apostas...');
  const [resNormais, resHS, resCancel] = await Promise.all([
    verificarResolucao(normalUsers, idsNormais),
    verificarResolucao(hsUsers, idsHS),
    verificarResolucao(cancelUsers, idsCancel),
  ]);

  stats.rabbit.ganhas   = resNormais.ganhas + resHS.ganhas;
  stats.rabbit.perdidas = resNormais.perdidas + resHS.perdidas;
  stats.rabbit.anuladas = resCancel.anuladas;

  const totalResolvidas = stats.rabbit.ganhas + stats.rabbit.perdidas + stats.rabbit.anuladas;
  const pendentesTotal  = resNormais.pendentes + resHS.pendentes + resCancel.pendentes;

  log('RESOLV', `Normais: ganhas=${resNormais.ganhas} perdidas=${resNormais.perdidas} pendentes=${resNormais.pendentes}`);
  log('RESOLV', `HS:      ganhas=${resHS.ganhas} perdidas=${resHS.perdidas} pendentes=${resHS.pendentes}`);
  log('RESOLV', `Cancel:  anuladas=${resCancel.anuladas} pendentes=${resCancel.pendentes}`);

  if (pendentesTotal > 0) {
    log('WARN', `${pendentesTotal} apostas ainda pendentes — a aguardar mais 4s...`);
    await sleep(4000);
  }

  // ── Sumário ──────────────────────────────────────────────────────────────────
  const totalSecs = (Date.now() - start) / 1000;
  console.log(`\n${c('1;36', '══════════════════════════════════════════════════')}`);
  console.log(c('1', ' SUMÁRIO FINAL'));
  console.log(`${c('36', '──────────────────────────────────────────────────')}`);
  console.log(` Duração total          : ${c('1', totalSecs.toFixed(1) + 's')}`);
  console.log(` Jogos criados          : ${stats.jogos.criados}/${totalJogos}  (falhas: ${stats.jogos.falhas})`);
  console.log(` Kafka propagados       : ${stats.jogos.kafkaPropagados}/${stats.jogos.criados}`);
  console.log(` Top-ups admin          : ${stats.topups} × ${TOPUP_AMOUNT}€`);
  console.log(` Apostas colocadas      : ${c('1', stats.apostas.colocadas)}  (falhas: ${stats.apostas.falhas})  throughput: ${stats.betsPerSecond}/s`);
  console.log(` RabbitMQ — ganhas      : ${c('32', stats.rabbit.ganhas)}`);
  console.log(` RabbitMQ — perdidas    : ${stats.rabbit.perdidas}`);
  console.log(` RabbitMQ — anuladas    : ${c('33', stats.rabbit.anuladas)}  (cancelamentos)`);
  console.log(` Total resolvidas       : ${c('1', totalResolvidas)}  pendentes: ${pendentesTotal}`);
  console.log(`${c('1;36', '══════════════════════════════════════════════════')}`);

  await printAnalytics();
}

main().catch(e => { console.error(c('91', 'Erro fatal:'), e); process.exit(1); });
