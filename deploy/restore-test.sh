#!/usr/bin/env bash
# Restore test (ROADMAP Faza 5): restores a backup dump into a scratch database next to the real one and
# compares the row count of every table. The real database is only read; the scratch database is dropped at the end.
#
#   deploy/restore-test.sh [dump-file]
#
# Without a dump file it first makes a fresh dump of the running database (same command as backup.sh).
# Production (on the VPS): uses deploy/docker-compose.yml and deploy/.env.
# Local dev database:      COMPOSE_FILE=deploy/docker-compose.dev.yml deploy/restore-test.sh
set -Eeuo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
COMPOSE_FILE="${COMPOSE_FILE:-$DEPLOY_DIR/docker-compose.yml}"
COMPOSE=(docker compose -f "$COMPOSE_FILE")
ENV_FILE="${ENV_FILE:-$DEPLOY_DIR/.env}"
[[ -f "$ENV_FILE" && "$COMPOSE_FILE" == "$DEPLOY_DIR/docker-compose.yml" ]] && COMPOSE+=(--env-file "$ENV_FILE")
SCRATCH="tds_restore_test"
WORK="$(mktemp -d)"
trap 'rm -rf "$WORK"; "${COMPOSE[@]}" exec -T db dropdb -U tds --if-exists "$SCRATCH" < /dev/null || true' EXIT

psql_in() { "${COMPOSE[@]}" exec -T db psql -U tds -d "$1" -v ON_ERROR_STOP=1 -At -c "$2" < /dev/null; }

DUMP="${1:-}"
if [[ -z "$DUMP" ]]; then
  DUMP="$WORK/tds.dump"
  "${COMPOSE[@]}" exec -T db pg_dump -U tds -d tds -Fc < /dev/null > "$DUMP"
  echo "dump: fresh ($(du -h "$DUMP" | cut -f1))"
else
  echo "dump: $DUMP ($(du -h "$DUMP" | cut -f1))"
fi
[[ -s "$DUMP" ]] || { echo "FAIL: dump is empty"; exit 1; }

"${COMPOSE[@]}" exec -T db dropdb -U tds --if-exists "$SCRATCH" < /dev/null
"${COMPOSE[@]}" exec -T db createdb -U tds "$SCRATCH" < /dev/null
"${COMPOSE[@]}" exec -T db pg_restore -U tds -d "$SCRATCH" --no-owner --exit-on-error < "$DUMP"
echo "restore: OK into $SCRATCH"

# Exact row counts per table (schema public), same query on both databases.
COUNT_SQL="SELECT string_agg(t || '=' || n, E'\n' ORDER BY t) FROM (
  SELECT table_name AS t,
         (xpath('/row/c/text()', query_to_xml(format('SELECT count(*) AS c FROM public.%I', table_name), false, true, '')))[1]::text AS n
  FROM information_schema.tables WHERE table_schema = 'public' AND table_type = 'BASE TABLE') x"
psql_in tds "$COUNT_SQL" > "$WORK/source.txt"
psql_in "$SCRATCH" "$COUNT_SQL" > "$WORK/restored.txt"

TABLES="$(wc -l < "$WORK/restored.txt" | tr -d ' ')"
ROWS="$(awk -F= '{s += $2} END {print s}' "$WORK/restored.txt")"
MIGRATION="$(psql_in "$SCRATCH" 'SELECT max(migration_id) FROM "__EFMigrationsHistory"')"
echo "restored: $TABLES tables, $ROWS rows, last migration $MIGRATION"

if [[ -n "${1:-}" ]]; then
  # An older dump is expected to differ from the live database; the restore itself is the test.
  echo "OK: restore of $DUMP works (row counts not compared with the live database)"
elif diff -u "$WORK/source.txt" "$WORK/restored.txt"; then
  echo "OK: every table has the same row count as the live database"
else
  echo "FAIL: row counts differ (see diff above)"
  exit 1
fi
