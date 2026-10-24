# RR1X24

RR1X24 sports platform based on the upstream IS-BetStrike demonstration project.

> **Important:** The upstream project is a fictional sports-betting demonstration and uses simulated match data. This code has not been verified as production-ready for live sports data, real-money payments, or commercial betting.

## Stack
- Angular frontend
- .NET services
- SQL Server
- Docker Compose
- Kafka and RabbitMQ

## Run locally
1. Install Docker and Docker Compose.
2. Copy the example environment file: `cp .env.example .env`.
3. Set strong, unique secrets and administrator credentials in `.env`.
4. Start services: `docker compose up --build`.
5. Use the frontend port shown in `compose.yaml`.

Never commit a real `.env` file, passwords, API keys, payment secrets, or production credentials.

## Upstream
Source repository: https://github.com/joaoesteves10/IS-BetStrike

Check the upstream project's license and comply with applicable laws and provider requirements before redistribution or deployment. Branding changes do not imply production readiness.
