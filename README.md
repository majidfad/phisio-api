# Phisio API (Backend)

ASP.NET Core 8 API for the Phisio physiotherapy platform.

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

Deploys API + PostgreSQL to `/opt/phisio-api` on the VPS.

**Deploy backend before the frontend repo** — this stack creates the shared Docker network `phisio_internal` used by the web container.

See [deploy/GITHUB_SECRETS.md](deploy/GITHUB_SECRETS.md) for CI/CD secrets.
