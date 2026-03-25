# API Resultados - Endpoints

Base path: `api/jogos`

## Regras de negocio

- Estados suportados:
  - `1` = Agendado
  - `2` = EmCurso
  - `3` = Finalizado
  - `4` = Cancelado
  - `5` = Adiado
- Transicoes validas de estado:
  - `Agendado -> EmCurso`
  - `EmCurso -> Finalizado`
  - `Agendado -> Cancelado`
  - `Agendado -> Adiado`
- Um jogo novo deve ser criado em `Agendado (1)`.
- Atualizacao de marcador so e permitida quando o estado efetivo do pedido e `EmCurso`.
- O registo em `resultado` so e criado/atualizado quando o jogo transita para `Finalizado`.
- Se um jogo finaliza sem marcador conhecido, e guardado `0-0` com `resultado = "0"` (desconhecido).
- `resultado` simbolico:
  - `1` = vitoria casa
  - `2` = vitoria fora
  - `X` = empate
  - `0` = desconhecido
- Remocao so e permitida quando o jogo esta em `Agendado`.
- `codigoJogo` tem formato obrigatorio `FUT-AAAA-JJNN` (ex.: `FUT-2026-0103`).

## Autenticacao e auditoria

- Endpoints de leitura (`GET /api/jogos` e `GET /api/jogos/{codigoJogo}`) sao publicos.
- Endpoints de escrita (`POST`, `PATCH`, `DELETE` em `api/jogos`) exigem header `X-API-Key`.
- A chave e lida de `RESULTADOS_API_KEY` no `.env`.
- Header opcional `X-Client-Id` pode ser enviado para identificar o consumidor nos logs.
- A API regista auditoria para operacoes de escrita com `ClientId`, IP remoto, fingerprint da API key, metodo, rota, status e duracao.
- A API key valida e lida de `RESULTADOS_API_KEY` no arranque da aplicacao.
- Se alterares a key no `.env`, tens de reiniciar/recriar o processo/container da `apiresultados` para a nova key passar a ser valida.

## 1) Criar jogo

`POST /api/jogos`

Cria um novo jogo no estado `Agendado`.

### Headers obrigatorios

- `X-API-Key: <valor da RESULTADOS_API_KEY>`

### Body (JSON)

```json
{
  "codigoJogo": "FUT-2026-0103",
  "dataHoraInicio": "2026-03-26T20:30:00Z",
  "equipaCasa": "Benfica",
  "equipaFora": "Porto",
  "tipoCompeticao": "Liga Portugal",
  "estadoAtual": 1,
  "golosCasa": 0,
  "golosVisitante": 0
}
```

### Respostas

- `201 Created`: jogo criado
- `401 Unauthorized`: API key em falta
- `403 Forbidden`: API key invalida
- `409 Conflict`: `codigoJogo` ja existe
- `400 Bad Request`: payload invalido ou `estadoAtual` diferente de `1`

## 2) Atualizar estado e/ou marcador

`PATCH /api/jogos/{codigoJogo}`

Atualiza estado e/ou marcador de um jogo existente.

### Headers obrigatorios

- `X-API-Key: <valor da RESULTADOS_API_KEY>`

### Exemplos de body

Atualizar estado para EmCurso:

```json
{
  "estadoAtual": 2
}
```

Atualizar marcador (apenas em jogo EmCurso):

```json
{
  "golosCasa": 1,
  "golosVisitante": 0
}
```

Atualizar estado e marcador no mesmo pedido (Agendado -> EmCurso):

```json
{
  "estadoAtual": 2,
  "golosCasa": 1,
  "golosVisitante": 0
}
```

### Respostas

- `200 OK`: atualizacao aplicada
- `401 Unauthorized`: API key em falta
- `403 Forbidden`: API key invalida
- `400 Bad Request`: payload invalido (ex.: falta um dos campos de golos)
- `404 Not Found`: jogo nao existe
- `409 Conflict`: transicao de estado invalida ou marcador fora de `EmCurso`

## 3) Listar jogos

`GET /api/jogos`

Lista todos os jogos, com filtros opcionais por data e estado.

### Query params opcionais

- `dataInicio` (DateTime)
- `dataFim` (DateTime)
- `estado` (1-5)

Exemplo:

`GET /api/jogos?estado=2&dataInicio=2026-03-26T00:00:00Z`

### Respostas

- `200 OK`: lista de jogos
- `400 Bad Request`: filtros invalidos (ex.: `dataInicio > dataFim`)

## 4) Obter jogo por codigo

`GET /api/jogos/{codigoJogo}`

Devolve os dados completos do jogo e resultado atual.

### Respostas

- `200 OK`: jogo encontrado
- `404 Not Found`: jogo nao existe

## 5) Remover jogo

`DELETE /api/jogos/{codigoJogo}`

Remove um jogo apenas se estiver em `Agendado`.

### Headers obrigatorios

- `X-API-Key: <valor da RESULTADOS_API_KEY>`

### Respostas

- `204 No Content`: removido com sucesso
- `401 Unauthorized`: API key em falta
- `403 Forbidden`: API key invalida
- `404 Not Found`: jogo nao existe
- `409 Conflict`: jogo nao esta em `Agendado`

## 6) Health

`GET /health`

Endpoint de liveness da API (publico).

### Resposta

- `200 OK`

```json
"ok"
```

## 7) Health stats

`GET /health/stats`

Devolve o numero total de registos nas tabelas `jogo` e `resultado`.

### Headers obrigatorios

- `X-API-Key: <valor da RESULTADOS_API_KEY>`

### Resposta

- `200 OK`
- `401 Unauthorized`: API key em falta
- `403 Forbidden`: API key invalida

```json
{
  "jogos": 18,
  "resultados": 18,
  "databaseReachable": true,
  "timestampUtc": "2026-03-25T17:10:45Z"
}
```

## OpenAPI e Scalar UI

A API expoe a documentacao em ambiente de desenvolvimento:

- Documento OpenAPI JSON: `/openapi/v1.json`
- UI interativa (Scalar): `/scalar`

As descricoes dos endpoints aparecem atraves dos comentarios XML no codigo (`JogosController` e contratos em `Contracts`).

## Testes rapidos no ficheiro .http

Foram adicionados requests prontos em `ApiResultados/ApiResultados.http` para validar:

- Cenario A (resultado conhecido): marcador em `EmCurso` + finalizacao, esperado `resultado` = `1`/`2`/`X`.
- Cenario B (resultado desconhecido): `Agendado -> EmCurso -> Finalizado` sem updates de marcador, esperado `resultado` = `0`.
