#!/usr/bin/env bash
set -euo pipefail

APP_DIR="/opt/phisio-api"
REPO_URL="${REPO_URL:-}"

echo "==> Creating application directory at ${APP_DIR}"
sudo mkdir -p "${APP_DIR}"
sudo chown "${USER}:${USER}" "${APP_DIR}"

if [[ -n "${REPO_URL}" ]]; then
  if [[ ! -d "${APP_DIR}/repo" ]]; then
    git clone "${REPO_URL}" "${APP_DIR}/repo"
  fi
  cp "${APP_DIR}/repo/deploy/docker-compose.prod.yml" "${APP_DIR}/"
  cp "${APP_DIR}/repo/deploy/.env.example" "${APP_DIR}/.env.example"
fi

if [[ ! -f "${APP_DIR}/.env" ]]; then
  cp "${APP_DIR}/.env.example" "${APP_DIR}/.env"
  echo "==> Created ${APP_DIR}/.env — edit secrets before first deploy"
fi

if ! groups "${USER}" | grep -q '\bdocker\b'; then
  sudo usermod -aG docker "${USER}"
  echo "==> Added ${USER} to docker group — log out and back in"
fi

cat <<EOF

Backend bootstrap complete.

Next:
1. Edit ${APP_DIR}/.env (POSTGRES_PASSWORD, JWT_SECRET_KEY).
2. Configure GitHub secrets (see deploy/GITHUB_SECRETS.md).
3. Deploy backend before frontend (creates shared network phisio_internal).

EOF
