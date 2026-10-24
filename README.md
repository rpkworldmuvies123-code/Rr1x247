# RR1X24

RR1X24 is a sports platform codebase based on the upstream IS-BetStrike demonstration project.

> **Important:** The upstream project describes itself as a fictional sports-betting demonstration. Match data is simulated. This code is not confirmed production-ready for live sports data, real-money payments, or commercial betting.

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
5. Open the frontend at the local port configured in `compose.yaml`.

Never commit a real `.env` file, passwords, API keys, payment secrets, or production credentials.

## Upstream
Source repository: https://github.com/joaoesteves10/IS-BetStrike

Check the upstream project's licence and comply with the laws and provider requirements in your jurisdiction before redistributing or deploying this software. No production betting/payment readiness is implied by this branding update.
