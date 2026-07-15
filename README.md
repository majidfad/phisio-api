# Phisio API (Backend)

Standalone backend repository for the Phisio physiotherapy platform.

**Location:** `c:\Users\Mahboubeh\source\repos\phisio-api`  
**Frontend repo:** [phisio-web](https://github.com/majidfad/phisio-web)

## Stack

- .NET 8, EF Core, PostgreSQL, JWT auth

## Local development

```bash
dotnet restore Phisio.sln
dotnet run --project Phisio.Api
```

## Docker

```bash
docker build -t phisio-api:local .
```

## Production deploy

Unified Docker Compose at `/opt/phisio` (postgres + api + web). This repo owns the compose file.

Push to `main` after CI passes — the Deploy workflow bootstraps the server, migrates legacy volumes, and updates `postgres` + `api`. Web CI (phisio-web) updates only `web`.

See [deploy/GITHUB_SECRETS.md](deploy/GITHUB_SECRETS.md) for required secrets and server prerequisites.

