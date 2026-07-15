# GitHub Secrets (Backend — owns unified compose)

| Secret | Description |
|--------|-------------|
| `DEPLOY_HOST` | VPS public IP |
| `DEPLOY_USER` | SSH user with Docker access |
| `DEPLOY_SSH_KEY` | Private SSH key |
| `POSTGRES_PASSWORD` | PostgreSQL password |
| `JWT_SECRET_KEY` | JWT signing key (min 32 chars) |
| `GHCR_PULL_TOKEN` | PAT with `read:packages` for server image pull |

Optional: `JWT_ISSUER`, `JWT_AUDIENCE`

Deploy path on server: `/opt/phisio` (single compose for postgres + api + web)

## What CI does (no manual server bootstrap)

On each successful API deploy to `main`, GitHub Actions will:

1. Create `/opt/phisio` if missing
2. Copy compose + `.env.example` + bootstrap script
3. Stop legacy `/opt/phisio-api` and `/opt/phisio-web` stacks
4. Migrate Postgres/upload volumes into `phisio_pgdata` / `phisio_uploads`
5. Create/merge `.env` (secrets from GitHub + legacy `.env` values)
6. Pull and start only `postgres` + `api` (leaves `web` alone)
7. Remove old API images

Web deploys (phisio-web) only update the `web` service afterward.

## Server prerequisites (one-time, outside this app)

- Docker + Docker Compose plugin installed
- `DEPLOY_USER` can SSH and run Docker
- Able to create `/opt/phisio` (passwordless `sudo mkdir`/`chown`, or pre-create the dir)
- GitHub secrets listed above configured in **both** repos (same host/key)
- Optionally open TCP **3000** in the firewall / cloud security group
