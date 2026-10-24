# RR1X24

RR1X24 sports platform based on the upstream IS-BetStrike demonstration project.

> **Important:** This is a fictional sports-betting demo. Match data is simulated. Do not use it for real-money betting or production payments without independent engineering, security, payments, and legal review.

## Stack
- Angular frontend
- .NET services
- SQL Server
- Docker Compose
- Kafka and RabbitMQ

## Run locally
1. Install Docker and Docker Compose.
2. Copy `.env.example` to `.env`.
3. Replace **every** `CHANGE_ME_*` value with a strong unique secret/password. Do not commit `.env`.
4. Start with `docker compose up --build`.
5. Frontend is mapped to port 4200 by default; check service health/logs before using it.

## Important configuration notes
- This is an adapted fork: verify the Compose file, SQL scripts, environment placeholders, and .NET connection strings together before running.
- Configure an admin account and rotate all secrets before use.
- The upstream project licence must be checked before redistribution or commercial use.
- Live cricket/match data and real-money payment integrations are **not** included or verified by a branding change.

## Upstream
https://github.com/joaoesteves10/IS-BetStrike
