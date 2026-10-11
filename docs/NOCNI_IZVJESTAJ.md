# Noćni izvještaj — 11.10.2026.

**Stao sam ranije: Docker Desktop je zapeo i ne pokreće nove kontejnere**, pa backend testovi (Testcontainers) ne
mogu da se izvrše. Detalji i tačno mjesto gdje sam stala: [NOCNI_LOG.md → STOP](NOCNI_LOG.md#stop--11102026-oko-0315-gdje-sam-stala).
Treba ti: restart Docker Desktopa (prekida i kontejnere projekta vodostaji, zato ga nisam radila sama).

## 1. Šta je urađeno

**Faza 3 — tvoja odluka (DROP):**
- `47a7ecb` Drop legacy equipment calibration columns — migracija `DropLegacyEquipmentCalibrationColumns` briše samo
  `last_calibration_date`, `next_calibration_date`, `calibration_certificate_file_id` (+ FK i indeks). Uklonjena mapiranja
  u kodu, DATA_MODEL §9 opisuje samo novo stanje, migracioni test provjerava prenos pa nestanak kolona. Seed već pravi
  kalibracije kroz `equipment_calibration` (svako mjerno sredstvo ima i datum i sljedeći datum). Primijenjeno i na dev bazu.

**Faza 4 — označena u ROADMAP-u:**
- `bc28729` **Brzi unos** `/brzi-unos` po UI §4: nalozi u proizvodnji sa pretragom i zadnja 3 korištena na vrhu, operater i
  mašine zapamćeni na uređaju (localStorage u try/catch — radi i kad je blokiran, pokriveno testom), stepperi 48 px sa
  `inputmode`, lot se pojavljuje tek kad treba i sam se izabere kad je jedini, `Gotovo` onemogućeno sa objašnjenjem bez
  prihvaćene završne kontrole (serverska provjera ostaje), toast "Spremljeno DP-…", nalog i operater ostaju, greške iznad
  dugmeta. Provjereno u browseru sa stvarnim API-jem.
- `3b928f6` **Liste na telefonu kao kartice** (sve liste + tabele u tabovima, dashboard) i **CSV export** na svih 10 lista
  (UTF-8 BOM, `;`, decimalni zarez, CRLF, prazno za nepoznato, zaštita od formula; test formata).
- `530de43` **PWA**: ručno pisan manifest + ikone (generisane skriptom bez paketa), bez service workera.
- `c82ebc1` **Tekstovi**: pročitano 716 tekstova u `web/src`; ispravke (login → korisnički nalog, "biće" → "bit će", greške
  koje kažu šta uraditi, navodnici) — lista u logu.
- Ekrani provjereni na 360×740 i 375×812 (56 provjera, lista u logu); jedini nalaz (tabovi naloga) ispravljen.
- `ac30028` ROADMAP + log (vidi tačku 3 — commitovano dok Docker nije radio).

**Faza 5 — nije započeta** (vidi tačku 3).

## 2. Noćne odluke za tvoju potvrdu

- **Radni sati u Brzom unosu su vidljivi i obavezni** (sklopljeni su samo utrošak i napomena). Razlog: server traži
  `LaborHours`, a prazno ≠ 0 (zlatno pravilo 6). → [UI.md §4](UI.md#4-brzi-unos-hala-mobilni--brzi-unos) "(nocna odluka — za potvrdu)".
- Lot u Brzom unosu se prikazuje i kad je samo utrošak > 0 (server ga tada traži); nalozi u listi = statusi iz DOMAIN §5.1;
  datum je uvijek danas (stariji datum ide kroz Dnevna proizvodnja → Novi). → [NOCNI_LOG.md, Faza 4 — odluke](NOCNI_LOG.md#faza-4--odluke)
- CSV: brojevi bez separatora hiljada, datumi `12.10.2026` (da ih Excel prepozna kao broj/datum). → isto mjesto u logu.
- Uklonila sam dva Caddy kontejnera koje sam sama napravila i koji su zapeli (izvan repoa; ništa drugo nisam dirala).

## 3. Šta nije urađeno ili nije uspjelo

- **Cijela Faza 5** (Notion import, lokalni restore test, UPUTSTVO.md, `/health`): Docker ne pokreće kontejnere →
  backend testovi ne rade → po pravilu ne smijem commitovati backend kod. Ništa od toga nije započeto ni ostavljeno
  napola (radni direktorij je čist).
- **Caddyfile** (`Content-Type` za manifest) nije provjeren kroz Caddy — upravo taj pokušaj je zapeo.
- **< 20 s po zapisu** u Brzom unosu je procjena po broju dodira (~9 prvi zapis, ~5 sljedeći), ne mjerenje u hali.
- **Greška:** commit `ac30028` (samo docs) je pushan dok su backend testovi padali zbog Dockera.

## 4. Stanje testova i builda

- Frontend: `npm run typecheck`, `npm run build` i `npm test` zeleno — **171 test** (zadnje pokretanje prije ovog izvještaja).
- Backend: `dotnet build` 0 upozorenja, 0 grešaka. `dotnet test` **trenutno ne može da se pokrene** (Docker) — svih 415
  pada na startu kontejnera. Zadnji zeleni puni backend run: commit `47a7ecb`, 433 testa (1 namjerno preskočen, ručni
  test performansi); backend kod se poslije toga nije mijenjao.

## 5. Otvorena pitanja za klijenta (DOMAIN §9) — i dalje stoje

1. Nalozi samo kroz plazmu, varenje ili savijanje — trebaju li statusi `Plazma` / `Varenje`?
2. Pravilo 5.3 (gotovi komadi tek nakon završne kontrole) — dnevno samo laser/presa, gotovi na kraju, ili djelimična
   završna kontrola po seriji?
3. Kako se mjeri utrošak materijala (table, kg, ostatak table)?
4. Postoji li format broja naloga?
5. Treba li polje za smjenu?
6. Šta iz sekcije "03 · Resursi i QMS" stvarno koriste?
7. Pragovi "Nisko" / "Ispod minimuma".
8. Ko su korisnici, koliko ih je, ko je Kontrolor?
