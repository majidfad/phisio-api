# GitHub Secrets (Backend — owns unified compose)

| Secret | Description |
|--------|-------------|
| `DEPLOY_HOST` | VPS public IP |
| `DEPLOY_USER` | SSH user with Docker access |
| `DEPLOY_SSH_KEY` | Private SSH key |
| `POSTGRES_PASSWORD` | PostgreSQL password |
| `JWT_SECRET_KEY` | JWT signing key (min 32 chars) |
| `GHCR_PULL_TOKEN` | PAT with `read:packages` so the VPS can pull from GHCR |

Optional: `JWT_ISSUER`, `JWT_AUDIENCE`

Deploy path: `/opt/phisio`

## Image strategy (best practice)

- CI builds and pushes to **GHCR**: `ghcr.io/<owner>/phisio-api:<git-sha>` (+ `:latest`)
- Server **never builds** app images and never uses `*:local`
- Deploy writes the SHA image into `/opt/phisio/.env` as `PHISIO_API_IMAGE` / `PHISIO_WEB_IMAGE`
- Compose uses `pull_policy: missing` (CI still runs an explicit `pull`)
- `web` is behind Compose profile `web` (API deploy starts only `postgres` + `api`)

## What CI does

1. Create `/opt/phisio` if missing  
2. Bootstrap (migrate legacy volumes, sanitize bad image refs)  
3. Login to GHCR on the server  
4. Set `PHISIO_API_IMAGE=ghcr.io/.../phisio-api:<sha>`  
5. Pull + start `postgres` + `api`  
6. Prune unused images  

Web CI sets `PHISIO_WEB_IMAGE=ghcr.io/.../phisio-web:<sha>` and runs `compose --profile web up`.

## Manual commands on the server

```bash
cd /opt/phisio
# After CI has written real GHCR refs into .env:
docker compose -f docker-compose.prod.yml --env-file .env up -d postgres api
docker compose -f docker-compose.prod.yml --env-file .env --profile web up -d --no-deps web
```

Do not set `PHISIO_API_IMAGE=phisio-api:local`.

## Connect to Postgres (SSH tunnel)

Postgres is published as `127.0.0.1:${POSTGRES_PORT:-5432}` on the VPS only (default **5432**).

```bash
# If POSTGRES_PORT=5432 (default):
ssh -L 5432:127.0.0.1:5432 DEPLOY_USER@DEPLOY_HOST

# If you set POSTGRES_PORT=5433 in /opt/phisio/.env:
ssh -L 5433:127.0.0.1:5433 DEPLOY_USER@DEPLOY_HOST
```

Host `127.0.0.1`, port = `POSTGRES_PORT`, db/user from `.env`, password = `POSTGRES_PASSWORD`. Do not open that port publicly.

## Server prerequisites

- Docker + Compose plugin  
- `DEPLOY_USER` can run Docker  
- Can create `/opt/phisio`  
- Firewall/cloud SG: TCP **3000** only (not 5432)  
- GHCR packages readable with `GHCR_PULL_TOKEN` (or public packages)
