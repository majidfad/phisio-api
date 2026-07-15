#!/usr/bin/env bash
# Idempotent bootstrap for /opt/phisio (safe to run from CI or manually).
set -euo pipefail

APP_DIR="${APP_DIR:-/opt/phisio}"
OLD_API_DIR="${OLD_API_DIR:-/opt/phisio-api}"
OLD_WEB_DIR="${OLD_WEB_DIR:-/opt/phisio-web}"
SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"

migrate_volume() {
  local old_name="$1"
  local new_name="$2"

  if ! docker volume inspect "${old_name}" >/dev/null 2>&1; then
    return 0
  fi

  if docker volume inspect "${new_name}" >/dev/null 2>&1; then
    echo "==> Volume ${new_name} already exists — skipping copy from ${old_name}"
    return 0
  fi

  echo "==> Migrating Docker volume ${old_name} -> ${new_name}"
  docker volume create "${new_name}" >/dev/null
  docker run --rm \
    -v "${old_name}:/from:ro" \
    -v "${new_name}:/to" \
    alpine:3.20 \
    sh -c 'cd /from && cp -a . /to/'
  echo "==> Migrated ${old_name} -> ${new_name}"
}

is_placeholder_image() {
  local value="$1"
  [[ -z "${value}" \
    || "${value}" == "phisio-api:local" \
    || "${value}" == "phisio-web:local" \
    || "${value}" == "phisio-web:pending" \
    || "${value}" == *:local \
    || "${value}" == *:pending \
    || "${value}" != ghcr.io/* ]]
}

merge_env_key() {
  local src_file="$1"
  local key="$2"
  local dest_file="$3"

  if [[ ! -f "${src_file}" ]]; then
    return 0
  fi

  local line
  line="$(grep -E "^${key}=" "${src_file}" 2>/dev/null || true)"
  if [[ -z "${line}" ]]; then
    return 0
  fi

  local incoming="${line#*=}"
  if [[ "${key}" == PHISIO_*_IMAGE ]] && is_placeholder_image "${incoming}"; then
    return 0
  fi

  if grep -qE "^${key}=" "${dest_file}" 2>/dev/null; then
    local existing
    existing="$(grep -E "^${key}=" "${dest_file}" | cut -d= -f2-)"
    if [[ -n "${existing}" ]] && ! is_placeholder_image "${existing}"; then
      if [[ "${existing}" != change-me-* && "${existing}" != "change-me-strong-password" ]]; then
        return 0
      fi
    fi
    # For non-image keys, keep real secrets; for images allow replace when placeholder.
    if [[ "${key}" != PHISIO_*_IMAGE ]]; then
      if [[ -n "${existing}" && "${existing}" != "change-me-strong-password" && "${existing}" != change-me-* ]]; then
        return 0
      fi
    fi
    sed -i "s|^${key}=.*|${line}|" "${dest_file}"
  else
    echo "${line}" >> "${dest_file}"
  fi
}

clear_bad_image_ref() {
  local key="$1"
  local file="$2"
  if ! grep -qE "^${key}=" "${file}" 2>/dev/null; then
    echo "${key}=" >> "${file}"
    return 0
  fi
  local value
  value="$(grep -E "^${key}=" "${file}" | cut -d= -f2-)"
  if is_placeholder_image "${value}"; then
    echo "==> Clearing invalid ${key}=${value}"
    sed -i "s|^${key}=.*|${key}=|" "${file}"
  fi
}

ensure_dir() {
  if [[ -d "${APP_DIR}" ]]; then
    return 0
  fi
  echo "==> Creating ${APP_DIR}"
  if mkdir -p "${APP_DIR}" 2>/dev/null; then
    return 0
  fi
  sudo mkdir -p "${APP_DIR}"
  sudo chown "${USER}:${USER}" "${APP_DIR}"
}

ensure_dir

if [[ -f "${SCRIPT_DIR}/docker-compose.prod.yml" && "${SCRIPT_DIR}" != "${APP_DIR}" ]]; then
  cp -f "${SCRIPT_DIR}/docker-compose.prod.yml" "${APP_DIR}/docker-compose.prod.yml"
fi
if [[ -f "${SCRIPT_DIR}/.env.example" && "${SCRIPT_DIR}" != "${APP_DIR}" ]]; then
  cp -f "${SCRIPT_DIR}/.env.example" "${APP_DIR}/.env.example"
fi

if [[ -f "${APP_DIR}/deploy/docker-compose.prod.yml" ]]; then
  mv -f "${APP_DIR}/deploy/docker-compose.prod.yml" "${APP_DIR}/docker-compose.prod.yml"
fi
if [[ -f "${APP_DIR}/deploy/.env.example" ]]; then
  mv -f "${APP_DIR}/deploy/.env.example" "${APP_DIR}/.env.example"
fi
if [[ -f "${APP_DIR}/deploy/bootstrap-server.sh" ]]; then
  mv -f "${APP_DIR}/deploy/bootstrap-server.sh" "${APP_DIR}/bootstrap-server.sh"
  chmod +x "${APP_DIR}/bootstrap-server.sh"
fi
rmdir "${APP_DIR}/deploy" 2>/dev/null || true

if [[ ! -f "${APP_DIR}/.env.example" && -f "${OLD_API_DIR}/.env.example" ]]; then
  cp "${OLD_API_DIR}/.env.example" "${APP_DIR}/.env.example"
fi

if [[ ! -f "${APP_DIR}/.env" ]]; then
  if [[ -f "${APP_DIR}/.env.example" ]]; then
    cp "${APP_DIR}/.env.example" "${APP_DIR}/.env"
  elif [[ -f "${OLD_API_DIR}/.env" ]]; then
    cp "${OLD_API_DIR}/.env" "${APP_DIR}/.env"
  else
    echo "Missing .env.example in ${APP_DIR} — CI must copy deploy/.env.example"
    exit 1
  fi
  echo "==> Created ${APP_DIR}/.env"
fi

if [[ -f "${OLD_API_DIR}/.env" ]]; then
  for key in POSTGRES_DB POSTGRES_USER POSTGRES_PASSWORD JWT_SECRET_KEY JWT_ISSUER JWT_AUDIENCE JWT_ACCESS_TOKEN_EXPIRATION_MINUTES PHISIO_API_IMAGE; do
    merge_env_key "${OLD_API_DIR}/.env" "${key}" "${APP_DIR}/.env"
  done
fi
if [[ -f "${OLD_WEB_DIR}/.env" ]]; then
  for key in PHISIO_WEB_IMAGE HTTP_PORT; do
    merge_env_key "${OLD_WEB_DIR}/.env" "${key}" "${APP_DIR}/.env"
  done
fi

if ! grep -qE '^COMPOSE_PROJECT_NAME=' "${APP_DIR}/.env"; then
  echo 'COMPOSE_PROJECT_NAME=phisio' >> "${APP_DIR}/.env"
fi
if ! grep -qE '^HTTP_PORT=' "${APP_DIR}/.env"; then
  echo 'HTTP_PORT=3000' >> "${APP_DIR}/.env"
else
  sed -i 's|^HTTP_PORT=.*|HTTP_PORT=3000|' "${APP_DIR}/.env"
fi
if ! grep -qE '^PHISIO_API_IMAGE=' "${APP_DIR}/.env"; then
  echo 'PHISIO_API_IMAGE=' >> "${APP_DIR}/.env"
fi
if ! grep -qE '^PHISIO_WEB_IMAGE=' "${APP_DIR}/.env"; then
  echo 'PHISIO_WEB_IMAGE=' >> "${APP_DIR}/.env"
fi

clear_bad_image_ref PHISIO_API_IMAGE "${APP_DIR}/.env"
clear_bad_image_ref PHISIO_WEB_IMAGE "${APP_DIR}/.env"

if command -v docker >/dev/null 2>&1; then
  echo "==> Stopping legacy stacks (if present) before volume migration"
  if [[ -f "${OLD_API_DIR}/docker-compose.prod.yml" ]]; then
    (cd "${OLD_API_DIR}" && docker compose -f docker-compose.prod.yml --env-file .env down || true)
  fi
  if [[ -f "${OLD_WEB_DIR}/docker-compose.prod.yml" ]]; then
    (cd "${OLD_WEB_DIR}" && docker compose -f docker-compose.prod.yml --env-file .env down || true)
  fi

  migrate_volume "phisio-api_phisio_pgdata" "phisio_pgdata"
  migrate_volume "phisio-api_phisio_uploads" "phisio_uploads"
fi

if command -v ufw >/dev/null 2>&1; then
  if sudo -n ufw allow 3000/tcp >/dev/null 2>&1; then
    echo "==> Allowed TCP 3000 via ufw"
  else
    echo "==> Skipping ufw (no passwordless sudo) — open port 3000 manually if needed"
  fi
fi

echo "==> Bootstrap complete: ${APP_DIR}"
