/**
 * Replica os 7 folders da colecção Postman Parte2-Kafka-RabbitMQ
 * usando fetch nativo do Node 22. Imprime pass/fail por request.
 */

const RESULTADOS = 'http://localhost:5204';
const APOSTAS    = 'http://localhost:5141';
const ANALYTICS  = 'http://localhost:5400';
const API_KEY    = 'Ra1MTLOcHOaJL8EXlAaKQ8PjGMrccSgr5w4yxg0c6QNRb1zP0WVAj81KcX6uQV0T';

let passed = 0, failed = 0;
const start = Date.now();

const c = (code, s) => `\x1b[${code}m${s}\x1b[0m`;
function ok(name)   { passed++; console.log(`  ${c('32','✓')} ${name}`); }
function fail(name, msg) { failed++; console.log(`  ${c('91','✗')} ${name}  ${c('93',msg)}`); }
function section(n) { console.log(`\n${c('1;36',`── ${n} ──`)}`); }

async function fj(url, opts = {}) {
  try {
    const r = await fetch(url, { signal: AbortSignal.timeout(8000), ...opts });
    const t = await r.text();
    let b; try { b = JSON.parse(t); } catch { b = t; }
    return { status: r.status, body: b };
  } catch(e) { return { status: 0, body: null, error: e.message }; }
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

// ── 1. Health Checks ──────────────────────────────────────────────────────────
section('1. Health Checks');

let r = await fj(`${RESULTADOS}/health`);
r.status === 200 ? ok('ApiResultados /health') : fail('ApiResultados /health', `${r.status}`);

r = await fj(`${RESULTADOS}/health/ready`);
r.status === 200 && r.body?.status === 'Healthy'
  ? ok(`ApiResultados /health/ready → sqlserver:${r.body.checks.sqlserver?.status} kafka:${r.body.checks.kafka?.status}`)
  : fail('ApiResultados /health/ready', JSON.stringify(r.body?.checks ?? r.status));

r = await fj(`${APOSTAS}/api/jogos`);
r.status === 200 ? ok('ApiApostas UP (GET /api/jogos)') : fail('ApiApostas UP', `${r.status}`);

r = await fj(`${APOSTAS}/health`);
r.status === 200 && r.body?.status === 'Healthy'
  ? ok(`ApiApostas /health → sql:${r.body.checks.sqlserver?.status} rabbit:${r.body.checks.rabbitmq?.status} kafka:${r.body.checks.kafka?.status}`)
  : fail('ApiApostas /health', JSON.stringify(r.body ?? r.status));

r = await fj(`${ANALYTICS}/api/analytics/resumo`);
r.status === 200 ? ok('AnalyticsService UP') : fail('AnalyticsService UP', `${r.status}`);

r = await fj(`${ANALYTICS}/health`);
r.status === 200 && r.body?.status === 'Healthy'
  ? ok(`AnalyticsService /health → kafka:${r.body.checks.kafka?.status} metricsstore:${r.body.checks.metricsstore?.status}`)
  : fail('AnalyticsService /health', JSON.stringify(r.body ?? r.status));

// ── 2. Auth ───────────────────────────────────────────────────────────────────
section('2. Auth');
const EMAIL = `postman-test-${Date.now()}@betstrike.pt`;
r = await fj(`${APOSTAS}/api/auth/registar`, {
  method: 'POST', headers: {'Content-Type':'application/json'},
  body: JSON.stringify({ email: EMAIL, password: 'Test123!', nomeCompleto: 'Postman Test' })
});
let token = '';
if (r.status === 200 && r.body?.token) { token = r.body.token; ok('Registar utilizador'); }
else { fail('Registar utilizador', `${r.status}`); }

r = await fj(`${APOSTAS}/api/auth/login`, {
  method: 'POST', headers: {'Content-Type':'application/json'},
  body: JSON.stringify({ email: EMAIL, password: 'Test123!' })
});
r.status === 200 && r.body?.token ? ok('Login') : fail('Login', `${r.status}`);
if (r.body?.token) token = r.body.token;

r = await fj(`${APOSTAS}/api/pagamentos/saldo`, { headers: { Authorization: `Bearer ${token}` } });
r.status === 200 && r.body?.SaldoAtual >= 50
  ? ok(`Saldo inicial: ${r.body.SaldoAtual} €`)
  : fail('Saldo inicial', `${r.status} ${JSON.stringify(r.body)}`);

// ── 3. Pipeline Kafka ─────────────────────────────────────────────────────────
section('3. Pipeline Kafka — Criar Jogo');
const COD = `FUT-2026-${String(Date.now()).slice(-4)}`;
r = await fj(`${RESULTADOS}/api/jogos`, {
  method: 'POST', headers: {'Content-Type':'application/json','X-API-Key':API_KEY},
  body: JSON.stringify({ codigoJogo: COD, equipaCasa: 'Benfica', equipaFora: 'Porto',
    tipoCompeticao: 'Primeira Liga', dataHoraInicio: '2026-12-15T20:00:00Z', estadoAtual: 1 })
});
r.status === 201 ? ok(`ApiResultados POST /api/jogos → ${r.body?.codigoJogo}`) : fail('Criar jogo', `${r.status} ${JSON.stringify(r.body)}`);

// aguardar Kafka
let jogoChegou = false;
for (let i = 0; i < 10; i++) {
  await sleep(500);
  const j = await fj(`${APOSTAS}/api/jogos/${COD}`, { headers: { Authorization: `Bearer ${token}` } });
  if (j.status === 200) { jogoChegou = true; break; }
}
jogoChegou ? ok(`Kafka propagado → ${COD} em ApiApostas`) : fail('Kafka propagação', 'timeout 5s');

// ── 4. Apostar + Analytics ────────────────────────────────────────────────────
section('4. Apostar + Analytics');
r = await fj(`${APOSTAS}/api/apostas`, {
  method: 'POST', headers: {'Content-Type':'application/json', Authorization:`Bearer ${token}`},
  body: JSON.stringify({ codigoJogo: COD, tipoAposta: '1', montanteApostado: 20 })
});
const apostaId = r.body?.id;
r.status === 201 && apostaId
  ? ok(`Aposta colocada id=${apostaId} odd=${r.body?.oddMomento}`)
  : fail('Colocar aposta', `${r.status} ${JSON.stringify(r.body)}`);

r = await fj(`${ANALYTICS}/api/analytics/resumo`);
r.status === 200 && r.body?.apostas
  ? ok(`Analytics resumo — apostas/min:${r.body.apostas.ultimoMinuto} volume:${r.body.volumeTotal}`)
  : fail('Analytics resumo', `${r.status}`);

r = await fj(`${ANALYTICS}/api/analytics/jogos/${COD}`);
r.status === 200 && r.body?.codigoJogo === COD
  ? ok(`Analytics jogo ${COD} — volume:${r.body.volumeApostado} exposicao:${r.body.exposicaoFinanceira}`)
  : fail('Analytics jogo', `${r.status}`);

r = await fj(`${ANALYTICS}/api/analytics/alertas`);
r.status === 200 && Array.isArray(r.body)
  ? ok(`Analytics alertas — ${r.body.length} alerta(s)`)
  : fail('Analytics alertas', `${r.status}`);

// ── 5. Pipeline RabbitMQ — Resolução ────────────────────────────────────────
section('5. Pipeline RabbitMQ — Resolução de Apostas');
const saldoAntes = (await fj(`${APOSTAS}/api/pagamentos/saldo`, { headers: { Authorization: `Bearer ${token}` } })).body?.SaldoAtual;

r = await fj(`${RESULTADOS}/api/jogos/${COD}`, {
  method: 'PATCH', headers: {'Content-Type':'application/json','X-API-Key':API_KEY},
  body: JSON.stringify({ estadoAtual: 2, golosCasa: 1, golosVisitante: 0 })
});
r.status === 200 && r.body?.estadoAtual === 2 ? ok('PATCH EmCurso (1-0)') : fail('PATCH EmCurso', `${r.status}`);

r = await fj(`${RESULTADOS}/api/jogos/${COD}`, {
  method: 'PATCH', headers: {'Content-Type':'application/json','X-API-Key':API_KEY},
  body: JSON.stringify({ estadoAtual: 3 })
});
r.status === 200 && r.body?.estadoAtual === 3 ? ok('PATCH Finalizado → RabbitMQ publicado') : fail('PATCH Finalizado', `${r.status}`);

await sleep(4000);

r = await fj(`${APOSTAS}/api/apostas`, { headers: { Authorization: `Bearer ${token}` } });
const aposta = r.body?.find?.(a => a.Id === apostaId);
aposta?.Estado === 2
  ? ok(`Aposta id=${apostaId} → Ganha (estado=${aposta.Estado} prémio=${aposta.PremioPotencial}€)`)
  : fail('Aposta resolvida', `estado=${aposta?.Estado ?? 'não encontrada'}`);

r = await fj(`${APOSTAS}/api/pagamentos/saldo`, { headers: { Authorization: `Bearer ${token}` } });
const saldoDepois = r.body?.SaldoAtual;
saldoDepois > saldoAntes
  ? ok(`Saldo atualizado ${saldoAntes} → ${saldoDepois} € (prémio recebido)`)
  : fail('Saldo após resolução', `${saldoAntes} → ${saldoDepois}`);

// ── 6. Cancelamento ───────────────────────────────────────────────────────────
section('6. Cancelamento — Aposta Anulada + Reembolso');
const COD2 = `FUT-2026-${String(Date.now()).slice(-4)}`;
await fj(`${RESULTADOS}/api/jogos`, {
  method: 'POST', headers: {'Content-Type':'application/json','X-API-Key':API_KEY},
  body: JSON.stringify({ codigoJogo: COD2, equipaCasa: 'Sporting', equipaFora: 'Braga',
    tipoCompeticao: 'Primeira Liga', dataHoraInicio: '2026-12-16T20:00:00Z', estadoAtual: 1 })
});
for (let i = 0; i < 10; i++) {
  await sleep(500);
  const j = await fj(`${APOSTAS}/api/jogos/${COD2}`, { headers: { Authorization: `Bearer ${token}` } });
  if (j.status === 200) break;
}

const saldoAntesCancel = (await fj(`${APOSTAS}/api/pagamentos/saldo`, { headers: { Authorization: `Bearer ${token}` } })).body?.SaldoAtual;

r = await fj(`${APOSTAS}/api/apostas`, {
  method: 'POST', headers: {'Content-Type':'application/json', Authorization:`Bearer ${token}`},
  body: JSON.stringify({ codigoJogo: COD2, tipoAposta: 'X', montanteApostado: 5 })
});
const apostaIdCancel = r.body?.id;
r.status === 201 ? ok(`Aposta colocada id=${apostaIdCancel}`) : fail('Aposta no jogo cancel', `${r.status}`);

r = await fj(`${RESULTADOS}/api/jogos/${COD2}`, {
  method: 'PATCH', headers: {'Content-Type':'application/json','X-API-Key':API_KEY},
  body: JSON.stringify({ estadoAtual: 4 })
});
r.status === 200 && r.body?.estadoAtual === 4 ? ok('PATCH Cancelado → RabbitMQ') : fail('PATCH Cancelado', `${r.status}`);

await sleep(4000);

r = await fj(`${APOSTAS}/api/apostas`, { headers: { Authorization: `Bearer ${token}` } });
const apostaCancel = r.body?.find?.(a => a.Id === apostaIdCancel);
apostaCancel?.Estado === 4 ? ok(`Aposta id=${apostaIdCancel} → Anulada`) : fail('Aposta anulada', `estado=${apostaCancel?.Estado}`);

r = await fj(`${APOSTAS}/api/pagamentos/saldo`, { headers: { Authorization: `Bearer ${token}` } });
Math.abs(r.body?.SaldoAtual - saldoAntesCancel) < 0.01
  ? ok(`Reembolso correto: saldo voltou a ${r.body.SaldoAtual} €`)
  : fail('Reembolso', `esperado ~${saldoAntesCancel} obteve ${r.body?.SaldoAtual}`);

// ── 7. Analytics Final ────────────────────────────────────────────────────────
section('7. Analytics Final');
r = await fj(`${ANALYTICS}/api/analytics/resumo`);
if (r.status === 200) {
  const d = r.body;
  const tv = d.taxasVitoria;
  ok(`Resumo final — apostas/5min:${d.apostas.ultimos5Minutos} users:${d.utilizadoresAtivos5Min} volume:${d.volumeTotal}€ margem:${d.margemPlataforma}€`);
  ok(`Taxas vitória — 1:${tv.ganhou1} X:${tv.ganhouX} 2:${tv.ganhou2} total:${tv.total}`);
} else fail('Resumo final', `${r.status}`);

r = await fj(`${ANALYTICS}/api/analytics/alertas`);
r.status === 200 ? ok(`Alertas — ${r.body.length} no total`) : fail('Alertas finais', `${r.status}`);

// ── Sumário ───────────────────────────────────────────────────────────────────
const total = ((Date.now() - start) / 1000).toFixed(1);
console.log(`\n${c('1;36','══════════════════════════════════════')}`);
console.log(c('1',` POSTMAN FLOW — SUMÁRIO`));
console.log(`${c('36','──────────────────────────────────────')}`);
console.log(` ${c('32',`✓ ${passed} passed`)}  ${failed ? c('91',`✗ ${failed} failed`) : c('32','0 failed')}  ${total}s`);
console.log(`${c('1;36','══════════════════════════════════════')}\n`);

if (failed > 0) process.exit(1);
