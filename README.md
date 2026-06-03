# BetStrike

Plataforma fictícia de apostas desportivas desenvolvida no âmbito da UC de Integração de Sistemas (M/EI @ UTAD). Simula o ecossistema completo de uma casa de apostas portuguesa: geração de jogos da Primeira Liga, apostas em tempo real, pagamentos e analytics.

## Arquitetura

| Serviço | Porta | Descrição |
|---|---|---|
| GeradorDados | 5300 | Simula jogos e publica resultados |
| ApiResultados | 5204 | Plataforma FPF; persiste na BD `betstrike-resultados` |
| ApiApostas | 5141 | Plataforma BetStrike; persiste na BD `betstrike-apostas` |
| ApiPagamentos | 5232 | Sistema de pagamentos; persiste na BD `betstrike-pagamentos` |
| AnalyticsService | 5400 | Consumidor Kafka para alertas e estatísticas |
| Frontend (Angular) | 4200 | SPA a consumir ApiApostas |

## Requisitos

- Docker + Docker Compose

## Configuração

Copiar o ficheiro de exemplo e preencher os valores:

```bash
cp .env.example .env
```

| Variável | Descrição |
|---|---|
| `MSSQL_SA_PASSWORD` | Password do SA do SQL Server |
| `RESULTADOS_DB_PASSWORD` | Password do utilizador `resultados` |
| `RESULTADOS_API_KEY` | Chave de autenticação da ApiResultados (`X-API-Key`) |
| `APOSTAS_DB_PASSWORD` | Password do utilizador `apostas` |
| `JWT_SECRET` | Segredo JWT (mínimo 32 caracteres) |
| `ADMIN_EMAIL` | Email da conta de administrador |
| `ADMIN_PASSWORD` | Password da conta de administrador |
| `PAGAMENTOS_DB_PASSWORD` | Password do utilizador `pagamentos` |
| `PAGAMENTOS_INTERNAL_KEY` | Chave interna ApiApostas → ApiPagamentos |
| `RABBITMQ_USER` | Utilizador RabbitMQ |
| `RABBITMQ_PASSWORD` | Password RabbitMQ |

Os restantes valores têm defaults funcionais no `.env.example`.

## Arrancar

```bash
docker compose up

# para rebuild forçado
docker compose up --build
```

A BD é inicializada automaticamente pelo serviço `db-init` no primeiro arranque.

## Documentação

As APIs estão documentadas com Scalar em `/scalar`. Cada API tem também um ficheiro `.http` na respetiva pasta para testes com o REST Client da IDE.  
O relatório do trabalho encontra-se na pasta `docs`.
