# GitHub Secrets (Backend)

| Secret | Description |
|--------|-------------|
| `DEPLOY_HOST` | VPS public IP |
| `DEPLOY_USER` | SSH user with Docker access |
| `DEPLOY_SSH_KEY` | Private SSH key |
| `POSTGRES_PASSWORD` | PostgreSQL password |
| `JWT_SECRET_KEY` | JWT signing key (min 32 chars) |
| `GHCR_PULL_TOKEN` | PAT with `read:packages` for server image pull |

Deploy path on server: `/opt/phisio-api`

Deploy **backend before frontend** — this stack creates the shared Docker network `phisio_internal`.
