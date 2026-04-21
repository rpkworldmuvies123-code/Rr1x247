# GeradorDados Web Portal

Portal web simples para controlar a simulação de jornadas da 1a Liga.

## O que faz

- Executa a **próxima jornada** disponível na API de Resultados.
- Respeita o protocolo de dados:
  - 9 jogos por jornada;
  - código `FUT-AAAA-JJNN`;
  - jornadas pares reutilizam os mesmos emparelhamentos da jornada anterior com inversão casa/fora.
- Exibe estado da última execução no portal.
- Exibe em tempo real:
  - jogos gerados (código, equipas, estado e marcador);
  - timeline de eventos;
  - resultado de cada envio para a API (status e detalhe).

## Endpoints internos

- `GET /` - UI do portal.
- `GET /api/estado` - estado da execução atual/ultima.
- `GET /api/config` - configuração base da UI.
- `POST /api/simulacao/iniciar` - inicia a próxima jornada.
- `POST /api/estado/limpar` - limpa timeline e tabela de jogos no portal (quando não ha execução em curso).

Exemplo de payload para iniciar:

```json
{
  "intervaloAtualizacaoSegundos": 10
}
```

## Configuração

No `appsettings*.json`:

- `ApiResultados:BaseUrl`
- `ApiResultados:ApiKey`
- `ApiResultados:ClientId`
- `Simulation:IntervaloAtualizacaoSegundos`

No Docker Compose:

- `GERADORDADOS_PORT` (default `5300`)

## Uso em Docker

Após subir os serviços, abrir no browser:

- `http://localhost:5300`

ou a porta definida em `GERADORDADOS_PORT`.

> Nota: usa apenas a porta do compose (`5300` por defeito) para aceder ao portal.

