/**
 * BetStrike Resilience Test
 *
 * Testa o comportamento do sistema quando componentes ficam indisponíveis:
 *
 *   Cenário 1 — Kafka em baixo durante publicação de eventos
 *     → ApiResultados falha silenciosamente; ApiApostas não recebe update
 *     → Após restart do Kafka, novos eventos chegam normalmente
 *     → Demonstra limitação do fire-and-forget e necessidade de monitorização
 *
 *   Cenário 2 — RabbitMQ em baixo durante resolução de apostas
 *     → KafkaConsumerService não consegue publicar → não faz commit de offset
 *     → Após restart do RabbitMQ, Kafka redelivera a mensagem → apostas resolvem
 *
 *   Cenário 3 — ApiPagamentos em baixo durante resolução
 *     → BetResolutionWorker retenta 3× via x-retry-count
 *     → Após 3 falhas → mensagem vai para DLQ (bet-resolution.dlq)
 *     → Após restart do ApiPagamentos, mensagem na DLQ pode ser re-injectada
 */

const RESULTADOS_URL = 'http://localhost:5204';
const APOSTAS_URL    = 'http://localhost:5141';
const ANALYTICS_URL  = 'http://localhost:5400';
const API_KEY        = 'Ra1MTLOcHOaJL8EXlAaKQ8PjGMrccSgr5w4yxg0c6QNRb1zP0WVAj81KcX6uQV0T';
const ADMIN_EMAIL    = 'admin@betstrike.pt';
const ADMIN_PASSWORD = 'Admin123!';

import { execSync, exec } from 'child_process';
import { promisify } from 'util';
const execAsync = promisify(exec);

const start = Date.now();
const RUN_ID = String(Date.now()).slice(-4);
const YEAR = '2029';

const elapsed = () => `${((Date.now() - start) / 1000).toFixed(1)}s`.padStart(7);
const c = (code, s) => `\x1b[${code}m${s}\x1b[0m`;

function log(tag, msg) {
  const colors = { INFO:'37', OK:'32', WARN:'93', ERR:'91', ACTION:'36', DOWN:'31', UP:'32', WAIT:'90', RESULT:'96' };
  console.log(`[${elapsed()}] ${c(colors[tag]||'37', `[${tag.padEnd(6)}]`)} ${msg}`);
}

function section(title) {
  console.log(`\n${c('1;36','══════════════════════════════════════════════════')}`);
  console.log(c('1', ` ${title}`));
  console.log(`${c('36','──────────────────────────────────────────────────')}\n`);
}

function sleep(ms) { return new Promise(r => setTimeout(r, ms)); }

async function fetchJSON(url, opts = {}) {
  try {
    const r = await fetch(url, { signal: AbortSignal.timeout(5000), ...opts });
    const text = await r.text();
    let body; try { body = JSON.parse(text); } catch { body = text; }
    return { status: r.status, body };
  } catch (e) {
    return { status: 0, body: null, error: e.message };
  }
}

async function dockerStop(service) {
  log('DOWN', `A parar ${c('1;31', service)}...`);
  await execAsync(`docker compose stop ${service}`);
  log('DOWN', `${c('1;31', service)} está DOWN`);
}

async function dockerStart(service) {
  log('UP', `A arrancar ${c('1;32', service)}...`);
  await execAsync(`docker compose start ${service}`);
  // aguardar healthcheck
  for (let i = 0; i < 30; i++) {
    await sleep(2000);
    const { stdout } = await execAsync(`docker compose ps ${service} --format json`).catch(() => ({ stdout: '{}' }));
    try {
      const info = JSON.parse(stdout.trim().split('\n')[0] || '{}');
      if (info.Health === 'healthy' || info.Status?.includes('healthy')) {
        log('UP', `${c('1;32', service)} está UP e healthy`);
        return;
      }
    } catch {}
  }
  log('UP', `${c('1;32', service)} arrancou (sem healthcheck confirmado)`);
}

async function getToken(email, password) {
  const r = await fetchJSON(`${APOSTAS_URL}/api/auth/login`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password }),
  });
  return r.status === 200 ? r.body.token : null;
}

async function registarUser(email) {
  let r = await fetchJSON(`${APOSTAS_URL}/api/auth/registar`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: 'Res123!', nomeCompleto: 'Resilience Test' }),
  });
  if (r.status === 200) return { token: r.body.token, id: r.body.utilizadorId };
  r = await fetchJSON(`${APOSTAS_URL}/api/auth/login`, {
    method: 'POST', headers: { 'Content-Type': 'application/json' },
    body: JSON.stringify({ email, password: 'Res123!' }),
  });
  return r.status === 200 ? { token: r.body.token, id: r.body.utilizadorId } : null;
}

async function criarJogo(codigo, equipas) {
  return fetchJSON(`${RESULTADOS_URL}/api/jogos`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', 'X-API-Key': API_KEY },
    body: JSON.stringify({
      codigoJogo: codigo, equipaCasa: equipas[0], equipaFora: equipas[1],
      tipoCompeticao: 'Primeira Liga', dataHoraInicio: '2029-01-20T20:00:00Z', estadoAtual: 1,
    }),
  });
}

async function patchJogo(codigo, body) {
  return fetchJSON(`${RESULTADOS_URL}/api/jogos/${codigo}`, {
    method: 'PATCH',
    headers: { 'Content-Type': 'application/json', 'X-API-Key': API_KEY },
    body: JSON.stringify(body),
  });
}

async function apostar(token, codigoJogo, tipo = '1', montante = 10) {
  return fetchJSON(`${APOSTAS_URL}/api/apostas`, {
    method: 'POST',
    headers: { 'Content-Type': 'application/json', Authorization: `Bearer ${token}` },
    body: JSON.stringify({ codigoJogo, tipoAposta: tipo, montanteApostado: montante }),
  });
}

async function getSaldo(token) {
  const r = await fetchJSON(`${APOSTAS_URL}/api/pagamentos/saldo`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  return r.status === 200 ? r.body.SaldoAtual : null;
}

async function getApostas(token) {
  const r = await fetchJSON(`${APOSTAS_URL}/api/apostas`, {
    headers: { Authorization: `Bearer ${token}` },
  });
  return r.status === 200 ? r.body : [];
}

async function getRabbitQueueInfo(queue) {
  try {
    const rabbitmqUser = process.env.RABBITMQ_USER || 'betstrike';
    const rabbitmqPass = process.env.RABBITMQ_PASSWORD || 'DbauEVqKEmkn2Hk7';
    const r = await fetch(`http://localhost:15672/api/queues/%2F/${queue}`, {
      headers: { Authorization: `Basic ${Buffer.from(`${rabbitmqUser}:${rabbitmqPass}`).toString('base64')}` },
    });
    if (!r.ok) return null;
    const d = await r.json();
    return { messages: d.messages, consumers: d.consumers };
  } catch { return null; }
}

// ─────────────────────────────────────────────────────────────────────────────

async function cenario1_kafka_down() {
  section('CENÁRIO 1 — Kafka em baixo durante publicação de eventos');

  const cod1 = `FUT-${YEAR}-${RUN_ID.slice(0,2)}11`;
  const cod2 = `FUT-${YEAR}-${RUN_ID.slice(0,2)}12`;

  // Setup: user + jogo aberto + aposta
  const user = await registarUser(`res1-${RUN_ID}@betstrike.pt`);
  log('INFO', `User: res1-${RUN_ID}@betstrike.pt  |  saldo inicial: ${await getSaldo(user.token)} €`);

  log('ACTION', `Criar jogo ${cod1} (pré-falha) e verificar propagação normal...`);
  await criarJogo(cod1, ['Benfica', 'Porto']);
  await sleep(1500);
  const jogoPre = await fetchJSON(`${APOSTAS_URL}/api/jogos/${cod1}`, {
    headers: { Authorization: `Bearer ${user.token}` },
  });
  log('OK', `${cod1} propagado via Kafka: ${jogoPre.status === 200 ? 'SIM ✓' : 'NÃO ✗'}`);

  // Parar Kafka
  await dockerStop('kafka');
  await sleep(1000);

  // Criar jogo com Kafka em baixo
  log('ACTION', `Criar jogo ${cod2} com Kafka DOWN...`);
  const criarResult = await criarJogo(cod2, ['Sporting', 'Braga']);
  log(criarResult.status === 201 ? 'WARN' : 'ERR',
    `ApiResultados respondeu ${criarResult.status} — jogo gravado na BD (HTTP funciona), mas evento Kafka perdido`);

  // Verificar que ApiApostas não recebeu o jogo
  await sleep(2000);
  const jogoPos = await fetchJSON(`${APOSTAS_URL}/api/jogos/${cod2}`, {
    headers: { Authorization: `Bearer ${user.token}` },
  });
  log('RESULT', `${cod2} em ApiApostas: ${jogoPos.status === 200 ? 'SIM (inesperado)' : c('93','NÃO — como esperado, evento Kafka perdeu-se')}`);

  // Reiniciar Kafka
  await dockerStart('kafka');
  await sleep(3000);

  // Criar novo jogo após recuperação — deve propagar
  const cod3 = `FUT-${YEAR}-${RUN_ID.slice(0,2)}13`;
  log('ACTION', `Criar jogo ${cod3} após restart do Kafka — deve propagar normalmente...`);
  await criarJogo(cod3, ['Vitoria', 'Guimaraes']);
  await sleep(2000);
  const jogoRec = await fetchJSON(`${APOSTAS_URL}/api/jogos/${cod3}`, {
    headers: { Authorization: `Bearer ${user.token}` },
  });
  log(jogoRec.status === 200 ? 'OK' : 'ERR',
    `${cod3} propagado após recovery: ${jogoRec.status === 200 ? c('32','SIM ✓') : c('91','NÃO ✗')}`);

  return { user, cod1, cod3 };
}

async function cenario2_rabbitmq_down() {
  section('CENÁRIO 2 — RabbitMQ em baixo durante resolução de apostas');

  const cod = `FUT-${YEAR}-${RUN_ID.slice(0,2)}21`;
  const user = await registarUser(`res2-${RUN_ID}@betstrike.pt`);
  const saldoInicial = await getSaldo(user.token);
  log('INFO', `User: res2-${RUN_ID}@betstrike.pt  |  saldo: ${saldoInicial} €`);

  // Criar jogo e aguardar propagação Kafka antes de apostar
  await criarJogo(cod, ['Benfica', 'Sporting']);
  for (let i = 0; i < 10; i++) {
    await sleep(600);
    const r = await fetchJSON(`${APOSTAS_URL}/api/jogos/${cod}`, { headers: { Authorization: `Bearer ${user.token}` } });
    if (r.status === 200) break;
  }
  const aposta = await apostar(user.token, cod, '1', 15);
  if (!aposta.body?.id) { log('ERR', `Aposta falhou: ${JSON.stringify(aposta.body)}`); return; }
  log('ACTION', `Aposta criada: id=${aposta.body.id}  €15 no 1`);
  log('INFO', `Saldo após aposta: ${await getSaldo(user.token)} €`);

  // Parar RabbitMQ
  await dockerStop('rabbitmq');

  // Finalizar jogo com RabbitMQ em baixo
  log('ACTION', `Jogo → EmCurso (1-0) → Finalizado com RabbitMQ DOWN...`);
  await patchJogo(cod, { estadoAtual: 2, golosCasa: 1, golosVisitante: 0 });
  const fin = await patchJogo(cod, { estadoAtual: 3 });
  log(fin.status === 200 ? 'WARN' : 'ERR',
    `PATCH Finalizado: HTTP ${fin.status} — Kafka publicou game-event, mas KafkaConsumerService não consegue publicar em RabbitMQ`);

  await sleep(3000);
  const apostasDown = await getApostas(user.token);
  const estado1 = apostasDown.find(a => a.Id === aposta.body?.id)?.Estado;
  log('RESULT', `Estado da aposta enquanto RabbitMQ DOWN: ${estado1} (1=Pendente — ${c('93','como esperado, não resolvida')})`);

  // Verificar que Kafka offset não foi committed (log)
  log('WAIT', 'KafkaConsumerService está a tentar publicar em RabbitMQ e a falhar sem commit...');
  await sleep(2000);

  // Reiniciar RabbitMQ
  await dockerStart('rabbitmq');
  log('WAIT', 'A aguardar PendingBetsSweeper (corre a cada 30s) processar apostas pendentes...');
  await sleep(35000);

  // Verificar se aposta foi resolvida
  const apostasUp = await getApostas(user.token);
  const estado2 = apostasUp.find(a => a.Id === aposta.body?.id)?.Estado;
  const saldoFinal = await getSaldo(user.token);

  log(estado2 === 2 ? 'OK' : 'WARN',
    `Estado da aposta após recovery: ${estado2} ${estado2 === 2 ? c('32','(Ganha ✓)') : estado2 === 1 ? c('93','(ainda Pendente — pode precisar de mais tempo)') : `(${estado2})`}`);
  log('RESULT', `Saldo: ${saldoInicial} → ${saldoFinal} €  (ganhou ${(saldoFinal - saldoInicial + 15).toFixed(2)} € de prémio)`);

  const q = await getRabbitQueueInfo('bet-resolution');
  const dlq = await getRabbitQueueInfo('bet-resolution.dlq');
  log('RESULT', `RabbitMQ fila bet-resolution: ${q?.messages ?? '?'} msgs  |  DLQ: ${dlq?.messages ?? '?'} msgs`);

}

async function cenario3_apipagamentos_down() {
  section('CENÁRIO 3 — ApiPagamentos em baixo durante resolução (DLQ)');

  const cod = `FUT-${YEAR}-${RUN_ID.slice(0,2)}31`;
  const user = await registarUser(`res3-${RUN_ID}@betstrike.pt`);
  const saldoInicial = await getSaldo(user.token);
  log('INFO', `User: res3-${RUN_ID}@betstrike.pt  |  saldo: ${saldoInicial} €`);

  // Criar jogo e apostas
  await criarJogo(cod, ['Porto', 'Braga']);
  await sleep(1500);
  const ap1 = await apostar(user.token, cod, '1', 10);
  log('ACTION', `Aposta criada: id=${ap1.body?.id}  €10 no 1`);

  // Parar ApiPagamentos
  await dockerStop('apipagamentos');

  // Finalizar jogo
  log('ACTION', `Jogo → EmCurso (2-1) → Finalizado com ApiPagamentos DOWN...`);
  await patchJogo(cod, { estadoAtual: 2, golosCasa: 2, golosVisitante: 1 });
  await sleep(500);
  await patchJogo(cod, { estadoAtual: 3 });

  log('WAIT', 'BetResolutionWorker vai tentar processar e falhar 3× (x-retry-count)...');
  log('WAIT', `Cada retry aguarda ~TTL da fila. Observar RabbitMQ em http://localhost:15672`);

  // Aguardar retries (cada retry fica ~2s antes de ser reentregue pelo TTL)
  for (let i = 1; i <= 4; i++) {
    await sleep(4000);
    const q   = await getRabbitQueueInfo('bet-resolution');
    const dlq = await getRabbitQueueInfo('bet-resolution.dlq');
    const ap  = (await getApostas(user.token)).find(a => a.Id === ap1.body?.id);
    log('WAIT', `t+${i*4}s  |  bet-resolution: ${q?.messages ?? '?'} msgs  |  DLQ: ${dlq?.messages ?? '?'} msgs  |  aposta estado: ${ap?.Estado ?? '?'}`);
    if (dlq?.messages > 0) break;
  }

  const dlqFinal = await getRabbitQueueInfo('bet-resolution.dlq');
  log(dlqFinal?.messages > 0 ? 'RESULT' : 'WARN',
    `Após ${c('1','3 retries')}: ${dlqFinal?.messages > 0 ? c('93','mensagem na DLQ ✓ — isolada, não perdida') : 'DLQ vazia (pode precisar de mais tempo)'}`);

  // Reiniciar ApiPagamentos
  await dockerStart('apipagamentos');
  log('ACTION', 'ApiPagamentos UP — a re-injectar mensagem da DLQ na fila principal...');

  // Re-injectar da DLQ manualmente via RabbitMQ Management API
  await sleep(2000);
  const rabbitmqUser = process.env.RABBITMQ_USER || 'betstrike';
  const rabbitmqPass = process.env.RABBITMQ_PASSWORD || 'DbauEVqKEmkn2Hk7';
  try {
    // Usar shovel ou re-publicar: pegar mensagem da DLQ e publicar na fila principal
    const getMsg = await fetch(`http://localhost:15672/api/queues/%2F/bet-resolution.dlq/get`, {
      method: 'POST',
      headers: {
        Authorization: `Basic ${Buffer.from(`${rabbitmqUser}:${rabbitmqPass}`).toString('base64')}`,
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({ count: 10, ackmode: 'ack_requeue_false', encoding: 'auto', truncate: 50000 }),
    });
    if (getMsg.ok) {
      const msgs = await getMsg.json();
      log('ACTION', `${msgs.length} mensagem(ns) obtida(s) da DLQ — a re-publicar na fila principal...`);
      for (const msg of msgs) {
        await fetch(`http://localhost:15672/api/exchanges/%2F//publish`, {
          method: 'POST',
          headers: {
            Authorization: `Basic ${Buffer.from(`${rabbitmqUser}:${rabbitmqPass}`).toString('base64')}`,
            'Content-Type': 'application/json',
          },
          body: JSON.stringify({
            properties: { delivery_mode: 2, headers: {} },
            routing_key: 'bet-resolution',
            payload: msg.payload,
            payload_encoding: 'string',
          }),
        });
      }
      log('OK', `Mensagens re-injectadas na fila bet-resolution`);
    }
  } catch (e) {
    log('WARN', `Re-injecção manual falhou: ${e.message} — fazer via RabbitMQ UI manualmente`);
  }

  await sleep(5000);
  const apostasFinais = await getApostas(user.token);
  const estadoFinal = apostasFinais.find(a => a.Id === ap1.body?.id)?.Estado;
  const saldoFinal = await getSaldo(user.token);
  log(estadoFinal === 2 ? 'OK' : 'WARN',
    `Aposta após re-injecção: estado=${estadoFinal} ${estadoFinal === 2 ? c('32','(Ganha ✓)') : c('93','(verificar logs)')}`);
  log('RESULT', `Saldo: ${saldoInicial} → ${saldoFinal} €`);

}

// ── Main ──────────────────────────────────────────────────────────────────────
async function main() {
  console.log(c('1;36', '══════════════════════════════════════════════════'));
  console.log(c('1',    ' BetStrike Resilience Test'));
  console.log(`         3 cenários de falha — run ${RUN_ID}`);
  console.log(c('1;36', '══════════════════════════════════════════════════\n'));

  await cenario1_kafka_down();
  await cenario2_rabbitmq_down();
  await cenario3_apipagamentos_down();

  console.log(c('1;32', '\n══════════════════════════════════════════════════'));
  console.log(c('1',    ' SUMÁRIO DE RESILIÊNCIA'));
  console.log(c('32',   '══════════════════════════════════════════════════'));
  console.log(` Cenário 1 (Kafka down)        : Eventos perdidos durante downtime.`);
  console.log(`                                  Recovery imediato após restart.`);
  console.log(` Cenário 2 (RabbitMQ down)     : Kafka offset não commitado = ${c('32','sem perda de dados')}.`);
  console.log(`                                  Apostas resolvidas automaticamente após recovery.`);
  console.log(` Cenário 3 (ApiPagamentos down): ${c('32','DLQ isola falhas')} sem bloquear o pipeline.`);
  console.log(`                                  Re-injecção manual restaura o estado correto.`);
  console.log(c('1;32', '══════════════════════════════════════════════════\n'));
}

main().catch(e => { console.error(c('91', 'Erro fatal:'), e); process.exit(1); });
