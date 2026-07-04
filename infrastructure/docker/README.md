# Local Development Environment

SQL Server 2022 (Developer edition) running in Docker for FactoryPulse.

## First-time setup

Copy the example env file and set a password:

    cp .env.example .env      # PowerShell: Copy-Item .env.example .env

Edit `.env` and set a strong `MSSQL_SA_PASSWORD`
(8+ chars, upper, lower, digit, symbol).

## Start SQL Server

    docker compose up -d

## Stop SQL Server (keeps data)

    docker compose down

## Stop and delete all data (fresh start)

    docker compose down -v

## Connection details

- Server:   localhost,1433
- User:     sa
- Password: value of MSSQL_SA_PASSWORD in .env