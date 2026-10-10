# Deploy — prvi put na VPS

Jedan server, Docker Compose sa tri kontejnera:

| Servis | Šta radi |
|---|---|
| `db` | PostgreSQL 17, podaci u Docker volumenu `pgdata`, port nije otvoren prema vani |
| `api` | .NET API (`deploy/api.Dockerfile`), migracije se primjenjuju same pri pokretanju |
| `caddy` | HTTPS (Let's Encrypt automatski), servira izgrađeni frontend, `/api/*` → `api:8080`, SPA fallback na `index.html` (`deploy/caddy.Dockerfile`, `deploy/Caddyfile`) |

Fajlovi (crteži, certifikati) i ključevi za login cookie su na disku servera u `TDS_DATA_DIR`
(bind mount), da ih `backup.sh` može direktno kopirati.

## 1. Šta treba prije

- VPS sa Ubuntu 24.04 (ili Debian 12), min. 2 GB RAM, 40 GB disk.
- Domen (npr. `erp.tds-products.ba`) i **DNS A zapis** (i AAAA ako server ima IPv6) koji pokazuje
  na IP servera. Bez toga Caddy ne može dobiti HTTPS certifikat.
- Na firewallu otvoreni portovi **22, 80, 443** (TCP) i **443/UDP** (HTTP/3). Ništa drugo.
- Hetzner Storage Box (ili drugi SSH/rsync cilj) za backup van servera — opcionalno za početak,
  ali bez njega backup postoji samo na istom serveru.

## 2. Server

```bash
# kao root na VPS-u
apt update && apt upgrade -y
apt install -y ca-certificates curl git rsync
curl -fsSL https://get.docker.com | sh          # Docker Engine + compose plugin

mkdir -p /opt/tds
git clone https://github.com/berbicanes/tds-erp.git /opt/tds/app

# podaci: api kontejner radi kao uid 1654 (APP_UID u .NET slici)
mkdir -p /opt/tds/data/files /opt/tds/data/keys /opt/tds/backup
chown -R 1654:1654 /opt/tds/data
```

## 3. Konfiguracija

```bash
cd /opt/tds/app/deploy
cp .env.example .env
chmod 600 .env
nano .env
```

Obavezno popuniti: `TDS_DOMAIN`, `POSTGRES_PASSWORD` (`openssl rand -base64 32`),
`TDS_ADMIN_USERNAME`, `TDS_ADMIN_PASSWORD` (min. 10 znakova, barem jedna cifra i jedno malo slovo).
`TDS_DATA_DIR=/opt/tds/data`, `BACKUP_DIR=/opt/tds/backup`.

Admin korisnik se kreira samo ako korisnik sa tim imenom još ne postoji. Nakon prve prijave
lozinku iz `.env` možeš obrisati (postojeći korisnik se nikad ne mijenja).

## 4. Pokretanje

```bash
cd /opt/tds/app/deploy
docker compose up -d --build
docker compose ps                 # sva tri servisa "running"/"healthy"
docker compose logs -f api        # "migrations applied", "Seeded Admin user …"
docker compose logs -f caddy      # "certificate obtained successfully"
```

Provjera: `https://<TDS_DOMAIN>` → ekran prijave → prijava Admin korisnikom.

## 5. Backup

`deploy/backup.sh` radi:
1. `pg_dump -Fc` baze u `BACKUP_DIR/db/tds-YYYYMMDD-HHMMSS.dump`
2. dnevni snapshot fajlova u `BACKUP_DIR/files/YYYYMMDD-HHMMSS/` (nepromijenjeni fajlovi su
   hard linkovi na prethodni snapshot — svaki snapshot je kompletan, a prostor troše samo novi fajlovi)
3. briše sve starije od 30 dana (`RETENTION_DAYS`)
4. `rsync -aH --delete` cijelog `BACKUP_DIR` na Storage Box. Ako `BACKUP_RSYNC_HOST`/`USER` nisu
   postavljeni, ostaje samo lokalni backup i u log ide upozorenje.

Log: `BACKUP_DIR/backup.log`.

### Hetzner Storage Box (jednom)

```bash
# na VPS-u, kao root
ssh-keygen -t ed25519 -f /root/.ssh/tds_backup -N ""
# javni ključ na Storage Box (port 23, korisnik npr. u123456):
cat /root/.ssh/tds_backup.pub | ssh -p 23 u123456@u123456.your-storagebox.de install-ssh-key
```
U Hetzner konzoli za Storage Box uključiti **SSH support**. U `.env`:
`BACKUP_RSYNC_HOST=u123456.your-storagebox.de`, `BACKUP_RSYNC_USER=u123456`,
`BACKUP_RSYNC_PORT=23`, `BACKUP_SSH_KEY=/root/.ssh/tds_backup`.

### Cron

```bash
crontab -e
# svaki dan u 02:15
15 2 * * * /opt/tds/app/deploy/backup.sh >/dev/null 2>&1
```

Ručno pokretanje i provjera: `/opt/tds/app/deploy/backup.sh && tail /opt/tds/backup/backup.log`.

### Vraćanje (restore)

```bash
cd /opt/tds/app/deploy
docker compose stop api
docker compose exec -T db dropdb -U tds tds
docker compose exec -T db createdb -U tds tds
docker compose exec -T db pg_restore -U tds -d tds --no-owner < /opt/tds/backup/db/tds-YYYYMMDD-HHMMSS.dump
rsync -a /opt/tds/backup/files/YYYYMMDD-HHMMSS/ /opt/tds/data/files/
chown -R 1654:1654 /opt/tds/data/files
docker compose start api
```
(Automatski restore test je u Fazi 5.)

## 6. Ažuriranje

```bash
cd /opt/tds/app && git pull
cd deploy && docker compose up -d --build
```
Migracije se primjenjuju same pri pokretanju `api` kontejnera. Prije ažuriranja pokreni `backup.sh`.
