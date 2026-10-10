#!/usr/bin/env bash
# Local development: database + API + frontend with one command.
#   ./dev.sh          start everything (Ctrl+C stops API and frontend; the database keeps running)
# Dev admin login comes from .env.dev (gitignored). Copy .env.dev.example on first use.
set -Eeuo pipefail
cd "$(dirname "${BASH_SOURCE[0]}")"

if [[ -f .env.dev ]]; then
  set -a; source .env.dev; set +a
else
  echo "Napomena: .env.dev ne postoji — Admin korisnik se neće kreirati. Kopiraj .env.dev.example u .env.dev."
fi

if ! docker info >/dev/null 2>&1; then
  echo "Docker nije pokrenut. Pokreni Docker Desktop pa ponovo ./dev.sh"
  exit 1
fi

echo "▶ Baza (Postgres 17 na :5433)"
docker compose -f deploy/docker-compose.dev.yml up -d --wait db >/dev/null 2>&1

if lsof -iTCP:5080 -sTCP:LISTEN >/dev/null 2>&1; then
  echo "▶ API već radi na :5080"
  API_PID=""
else
  echo "▶ API (:5080) — log u .dev-api.log"
  dotnet run --project src/Tds.Api --launch-profile http > .dev-api.log 2>&1 &
  API_PID=$!
  trap '[[ -n "$API_PID" ]] && kill "$API_PID" 2>/dev/null; pkill -P "$API_PID" 2>/dev/null || true' EXIT
  for _ in $(seq 1 90); do
    if curl -s -o /dev/null http://localhost:5080/api/me; then break; fi
    if ! kill -0 "$API_PID" 2>/dev/null; then
      echo "API se nije pokrenuo. Zadnje linije iz .dev-api.log:"; tail -20 .dev-api.log; exit 1
    fi
    sleep 1
  done
  echo "  API spreman"
fi

echo "▶ Frontend (http://localhost:5173)"
cd web
[[ -d node_modules ]] || npm install
npm run dev
