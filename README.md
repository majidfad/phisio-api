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

Unified stack at `/opt/phisio`. Images are built in CI and pulled from **GHCR** (SHA tags in `.env`).

Push `main` → Deploy bootstraps the server, migrates data if needed, pulls `ghcr.io/.../phisio-api:<sha>`, starts `postgres` + `api`. Web CI updates the `web` profile only.

See [deploy/GITHUB_SECRETS.md](deploy/GITHUB_SECRETS.md).


