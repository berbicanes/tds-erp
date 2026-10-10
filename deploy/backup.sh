#!/usr/bin/env bash
# Daily backup: pg_dump + uploaded files, 30-day rotation, copy off-server (Hetzner Storage Box via rsync/SSH).
# Run on the VPS from cron as root, e.g.:  15 2 * * * /opt/tds/app/deploy/backup.sh
# Works without an off-server target: then it keeps local backups only and logs a warning.
set -Eeuo pipefail

DEPLOY_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
ENV_FILE="${ENV_FILE:-$DEPLOY_DIR/.env}"
if [[ -f "$ENV_FILE" ]]; then
  set -a
  # shellcheck disable=SC1090
  source "$ENV_FILE"
  set +a
fi

: "${BACKUP_DIR:?BACKUP_DIR is not set}"
: "${TDS_DATA_DIR:?TDS_DATA_DIR is not set}"
RETENTION_DAYS="${RETENTION_DAYS:-30}"
COMPOSE=(docker compose -f "$DEPLOY_DIR/docker-compose.yml" --env-file "$ENV_FILE")
STAMP="$(date +%Y%m%d-%H%M%S)"
DB_DIR="$BACKUP_DIR/db"
FILES_DIR="$BACKUP_DIR/files"
LOG="$BACKUP_DIR/backup.log"

mkdir -p "$DB_DIR" "$FILES_DIR"
log() { echo "$(date '+%Y-%m-%d %H:%M:%S') [$1] $2" | tee -a "$LOG"; }
trap 'log ERROR "backup failed at line $LINENO"' ERR

log INFO "backup $STAMP started"

# 1. Database: custom-format dump (restore with pg_restore). Written to a temp name first.
DUMP="$DB_DIR/tds-$STAMP.dump"
"${COMPOSE[@]}" exec -T db pg_dump -U tds -d tds -Fc < /dev/null > "$DUMP.part"
if [[ ! -s "$DUMP.part" ]]; then
  rm -f "$DUMP.part"
  log ERROR "pg_dump produced an empty file"
  exit 1
fi
mv "$DUMP.part" "$DUMP"
log INFO "database dump $(basename "$DUMP") ($(du -h "$DUMP" | cut -f1))"

# 2. Files: daily snapshot. Unchanged files are hard links to the previous snapshot,
#    so every snapshot is complete but only new files take space.
SNAPSHOT="$FILES_DIR/$STAMP"
LATEST="$(find "$FILES_DIR" -mindepth 1 -maxdepth 1 -type d | sort | tail -n 1 || true)"
if [[ -d "$TDS_DATA_DIR/files" ]]; then
  if [[ -n "$LATEST" ]]; then
    rsync -a --link-dest="$LATEST" "$TDS_DATA_DIR/files/" "$SNAPSHOT/"
  else
    rsync -a "$TDS_DATA_DIR/files/" "$SNAPSHOT/"
  fi
  log INFO "files snapshot $STAMP ($(find "$SNAPSHOT" -type f | wc -l | tr -d ' ') files)"
else
  log WARN "files directory $TDS_DATA_DIR/files does not exist, skipped"
fi

# 3. Rotation (local), by the timestamp in the name (rsync -a copies directory mtimes, so mtime is not reliable).
if CUTOFF="$(date -d "-$RETENTION_DAYS days" +%Y%m%d-%H%M%S 2>/dev/null)"; then :; else
  CUTOFF="$(date -v-"$RETENTION_DAYS"d +%Y%m%d-%H%M%S)" # BSD/macOS date (local testing)
fi
for f in "$DB_DIR"/tds-*.dump; do
  [[ -e "$f" ]] || continue
  name="$(basename "$f" .dump)"
  if [[ "${name#tds-}" < "$CUTOFF" ]]; then rm -f "$f"; log INFO "deleted old $f"; fi
done
for d in "$FILES_DIR"/*/; do
  [[ -d "$d" ]] || continue
  name="$(basename "$d")"
  if [[ "$name" < "$CUTOFF" ]]; then rm -rf "$d"; log INFO "deleted old $d"; fi
done

# 4. Off-server copy. --delete mirrors the local rotation; -H keeps snapshot hard links.
if [[ -z "${BACKUP_RSYNC_HOST:-}" || -z "${BACKUP_RSYNC_USER:-}" ]]; then
  log WARN "BACKUP_RSYNC_HOST / BACKUP_RSYNC_USER not set: backup is ONLY on this server"
else
  SSH="ssh -p ${BACKUP_RSYNC_PORT:-23} -o BatchMode=yes -o StrictHostKeyChecking=accept-new"
  [[ -n "${BACKUP_SSH_KEY:-}" ]] && SSH="$SSH -i $BACKUP_SSH_KEY"
  rsync -aH --delete --exclude 'backup.log' -e "$SSH" \
    "$BACKUP_DIR/" "$BACKUP_RSYNC_USER@$BACKUP_RSYNC_HOST:${BACKUP_RSYNC_PATH:-tds-backup}/"
  log INFO "copied to $BACKUP_RSYNC_USER@$BACKUP_RSYNC_HOST:${BACKUP_RSYNC_PATH:-tds-backup}"
fi

log INFO "backup $STAMP finished"
