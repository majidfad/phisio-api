# Server migration (VPS IP change)

Current production VPS: **85.198.15.132**  
Previous VPS (decommission after cutover): **45.59.114.213**

The app is served at **https://zivan.me** via Cloudflare. The VPS IP is not hardcoded in application code; it lives in GitHub Actions secrets and DNS.

## Checklist

### 1. Prepare the new VPS (`85.198.15.132`)

```bash
# As root on the new server
apt update && apt install -y docker.io docker-compose-plugin
usermod -aG docker DEPLOY_USER   # same user as GitHub secret DEPLOY_USER
mkdir -p /opt/phisio
chown DEPLOY_USER:DEPLOY_USER /opt/phisio
```

- Add the **public half** of `DEPLOY_SSH_KEY` to `~/.ssh/authorized_keys` for `DEPLOY_USER`.
- Allow inbound **TCP 80** (Cloudflare → nginx proxy). Do **not** expose Postgres publicly.

### 2. Copy data from the old server

On the **old** server (`45.59.114.213`), with the stack running or stopped cleanly:

```bash
cd /opt/phisio

# Backup Postgres
docker compose -f docker-compose.prod.yml --env-file .env exec -T postgres \
  pg_dump -U postgres -Fc phisio > /tmp/phisio.dump

# Backup uploads + env
docker run --rm -v phisio_uploads:/data -v /tmp:/backup alpine \
  tar czf /backup/phisio_uploads.tar.gz -C /data .
cp .env /tmp/phisio.env
```

Copy to your machine, then to the new server:

```bash
scp root@45.59.114.213:/tmp/phisio.{dump,env} /tmp/phisio_uploads.tar.gz DEPLOY_USER@85.198.15.132:/tmp/
```

### 3. Restore on the new server

```bash
ssh DEPLOY_USER@85.198.15.132
sudo mkdir -p /opt/phisio && sudo chown $USER:$USER /opt/phisio
cp /tmp/phisio.env /opt/phisio/.env
```

Trigger one API deploy from GitHub (step 4) so compose files land in `/opt/phisio`, **or** copy them manually from this repo's `deploy/` folder.

Then restore volumes:

```bash
cd /opt/phisio

# Postgres volume
docker compose -f docker-compose.prod.yml --env-file .env up -d postgres
sleep 15
docker compose -f docker-compose.prod.yml --env-file .env exec -T postgres \
  pg_restore -U postgres -d phisio --clean --if-exists < /tmp/phisio.dump

# Uploads volume
docker volume create phisio_uploads
docker run --rm -v phisio_uploads:/data -v /tmp:/backup alpine \
  tar xzf /backup/phisio_uploads.tar.gz -C /data
```

### 4. Update GitHub secrets (both repos)

In **phisio-api** and **phisio-web** → Settings → Secrets and variables → Actions:

| Secret | New value |
|--------|-----------|
| `DEPLOY_HOST` | `85.198.15.132` |

Keep `DEPLOY_USER`, `DEPLOY_SSH_KEY`, `POSTGRES_PASSWORD`, `JWT_SECRET_KEY`, and `GHCR_PULL_TOKEN` unchanged unless you rotated them.

If you generated a **new SSH key** for the new VPS, update `DEPLOY_SSH_KEY` in both repos.

### 5. Update Cloudflare DNS

For **zivan.me** and **www.zivan.me**:

- Change the **A record** from `45.59.114.213` → `85.198.15.132`
- Proxy status: **Proxied** (orange cloud), same as before
- SSL/TLS mode: **Full** or **Full (strict)** (unchanged)

Wait for DNS propagation (usually minutes with Cloudflare).

### 6. Deploy

Push to `main` on **phisio-api** (CI → deploy), then **phisio-web**, **or** re-run the latest successful Deploy workflows from the Actions tab.

Manual fallback on the new server:

```bash
cd /opt/phisio
docker compose -f docker-compose.prod.yml --env-file .env up -d postgres api
docker compose -f docker-compose.prod.yml --env-file .env --profile web up -d --no-deps web proxy
```

### 7. Verify

```bash
curl -fsS https://zivan.me/healthz
curl -fsS https://zivan.me/api/health
```

Log in to the app and confirm uploads / patient data.

### 8. Update local tools

- SSH: `ssh DEPLOY_USER@85.198.15.132`
- Postgres tunnel: `ssh -L 15433:127.0.0.1:5432 DEPLOY_USER@85.198.15.132` (adjust local port if 5433 is taken)
- Bitvise / pgAdmin: point host to **85.198.15.132**

### 9. Decommission old server

After 24–48 hours of stable traffic:

```bash
ssh root@45.59.114.213
cd /opt/phisio && docker compose -f docker-compose.prod.yml --env-file .env --profile web down
docker compose -f docker-compose.prod.yml --env-file .env down -v   # only if backups are verified
```

## What is *not* in git

These must be updated outside the repo:

- GitHub `DEPLOY_HOST` secret (both repos)
- Cloudflare A record
- Local SSH / tunnel configs
- Server `/opt/phisio/.env` (copy from old server or let CI merge secrets)

Domain **zivan.me** in `deploy/proxy.conf.template` stays the same; only the origin IP behind Cloudflare changes.
