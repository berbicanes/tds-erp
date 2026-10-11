# Noćni log (autonomni rad)

## Nastavak 11.10.2026. (dan) — Docker restartovan

- `dotnet test` nakon restarta: 433 (432 prošlo, 1 namjerno preskočen). Zapeli Ryuk kontejneri su nestali s restartom,
  pa nije bilo šta da se ukloni.
- Radni sati u Brzom unosu: obavezni, zadnja spremljena vrijednost se pamti na uređaju (`f1b7be0`), UI §4 bez oznake.
- Caddyfile: `caddy validate` u kontejneru (`tds-erp-caddy` slika) → "Valid configuration". Putanja repoa i dalje ima
  razmak ("tds erp"), pa sam umjesto bind mounta Caddyfile iz `.tmp/` poslala kroz stdin (`docker run -i … sh -c 'cat >
  /tmp/Caddyfile && caddy validate …'`) — nijedna putanja sa hosta ne ide u kontejner. Kontejner je `--rm`, nije ostao.

## Faza 5 — plan

1. `/api/health` (anoniman): baza (upit) i disk (slobodan prostor na storage rootu), bez verzija, putanja i brojeva.
   Docker healthcheck i rotacija logova u `docker-compose.yml` (json-file `max-size`/`max-file`, bez novih paketa).
2. Lokalni restore test: `pg_dump` dev baze → nova prazna baza u TDS dev kontejneru → `pg_restore` → poređenje broja
   redova po tabelama. Skripta `deploy/restore-test.sh`, rezultat u log.
3. `docs/UPUTSTVO.md`: jedna strana — brzi unos, prijem lota, status naloga.
4. Notion import: admin komanda `import-notion <folder> --dry-run | --apply`, jedna transakcija, izvještaj
   (duplikati, konflikti, nepoznate enum vrijednosti, prazna obavezna polja), mapiranje po NOTION_SOURCE.md,
   početno stanje kao `Korekcija` "Početno stanje iz Notiona", testovi na sintetičkim CSV fixtureima.
   ROADMAP: "urađeno, netestirano sa stvarnim podacima".
5. Na kraju `docs/NOCNI_IZVJESTAJ.md`: pregled svih faza + lista svega što čeka VPS, domen ili klijenta.

### Faza 5 — odluke

- **Health** je `/api/health`, ne `/health`: Caddy na API šalje samo `/api/*`, sve ostalo ide SPA-u. Anoniman, GET bez
  izmjena. Odgovor samo `status`, `database`, `disk` (`ok` / `greska` / `malo-prostora`), 503 kad nešto nije ok. Prag
  diska 1 GiB (`Health:MinFreeDiskBytes`), mjeri se na storage rootu fajlova (isti disk kao baza na jednom VPS-u).
- **Logovi sa rotacijom** kroz Docker `json-file` (5 × 10 MB po servisu) — bez Seriloga (novi paket). Docker
  healthcheck za `api` nije dodan: .NET slika nema `curl`/`wget`; vanjski uptime ping je pravi monitor (čeka domen).
- **Količina prijema za lotove iz Notiona.** Notion lot nema brojčanu količinu prijema (samo tekst `Dimenzije /
  količina`), a uputa kaže da početno stanje ulazi kao `Korekcija`. `ReceivedQty` je bio obavezan > 0 i pravi `Prijem`.
  Opcije: (a) `ReceivedQty` = trenutno stanje — izmišljena količina prijema (zlatno pravilo 6); (b) 0 — "nepoznato =
  nula"; (c) `ReceivedQty` nullable samo za uvezene lotove, UI "—". Izabrano (c): jedina iskrena opcija, nova migracija
  (`AlterColumn` → nullable, bez gubitka podataka), `Down` odbija vraćanje dok postoje lotovi sa nepoznatom količinom
  (da nepoznato ne postane 0). Prijem u aplikaciji i dalje traži > 0. DATA_MODEL §6 "(nocna odluka — za potvrdu)".
- **Mapiranje importa** je zapisano u `NOTION_SOURCE.md` „Import” prije koda (ključevi, formati, šta je greška,
  duplikat, upozorenje). Odluke unutar mapiranja: `--apply` ne upisuje ništa ako postoji ijedna greška (sve ili ništa);
  dry-run izvrši upis u transakciji i vrati je nazad; broj naloga iz Notiona se zadržava (DOMAIN §9.4 još otvoreno);
  status naloga se uvozi bez provjere prelaza; nepoznata osoba u neobaveznom polju = upozorenje, u obaveznom = greška;
  brojevi samo sa decimalnom tačkom (zarez je dvosmislen: `1,234`).
- **Restore test** (`deploy/restore-test.sh`): lokalno na dev bazi prošao — svjež dump i postojeći dump fajl,
  29 tabela, 197 redova, isti broj redova kao u živoj bazi, zadnja migracija `…DropLegacyEquipmentCalibrationColumns`.
  Privremena baza `tds_restore_test` se briše na kraju (provjereno: ostale su samo `tds` i sistemske).

## STOP — 11.10.2026. oko 03:15 (riješeno restartom Dockera, vidi gore)

**Razlog:** Docker Desktop više ne pokreće nove kontejnere. Svaki novi kontejner ostaje u stanju "Created":
moja dva pokušaja provjere Caddyfile-a (03:00, 03:03) i Testcontainers Ryuk kontejneri iz testova. Postojeći kontejneri
(TDS dev baza, vodostaji) i dalje rade. U logu Docker VM-a zadnji `POST …/start` je u 00:56 UTC, dakle zahtjevi za start
ne stižu do VM-a: zapelo je na strani Docker Desktopa na Macu. Disk nije pun (Data volumen: 22 GiB slobodno; Docker VM:
slike 28 GB). Najvjerovatniji okidač je moj `docker run` sa bind mountom putanje koja ima razmak ("tds erp").
Prije toga (02:50) svi testovi su prolazili.

**Posljedica:** backend testovi (Testcontainers) ne mogu da se pokrenu — svih 415 pada na startu kontejnera, ne zbog koda.
Pravilo je "build i svi testovi zeleni prije svakog commita", pa backend rad (cijela Faza 5) ne mogu da commitujem.
Popravka traži restart Docker Desktopa — to je izvan repoa i prekida i kontejnere projekta vodostaji, pa to ne radim
bez tvog odobrenja. Po pravilu "nešto ugrožava okruženje → STANI" stajem.

**Šta nisam dirala:** ništa izvan repoa, osim što sam uklonila dva Caddy kontejnera koje sam sama napravila i koji su
zapeli (03:00 `lucid_cartwright`, 03:03 `tds-caddy-adapt`). Dva zapela `testcontainers-ryuk-*` kontejnera iz mojih
test-pokretanja ostavila sam (bezopasni su; ukloni ih ili restartuj Docker Desktop kad odlučiš).

**Greška koju sam napravila:** commit `ac30028` (samo ROADMAP i ovaj log, bez koda) je pushan dok su backend testovi
padali — u lancu komandi `grep` je vratio uspjeh pa se commit izvršio. Kod se u tom commitu nije mijenjao; backend kod se
nije mijenjao od `47a7ecb`, gdje je cijeli backend suite bio zelen (433, 1 namjerno preskočen).

**Sljedeće (kad Docker radi):**
1. Restart Docker Desktopa (ti), pa `dotnet test` — očekivano 433 zeleno.
2. Faza 5 po planu iz upute: Notion import (admin komanda, `--dry-run`, jedna transakcija, izvještaj o konfliktima,
   sintetički CSV fixture), lokalni restore test backupa, `docs/UPUTSTVO.md`, `/health`.
3. Provjeriti Caddyfile (`caddy adapt`) kopiranjem u putanju bez razmaka.

## Faza 4 — plan

1. Brzi unos `/brzi-unos` (UI §4): nalozi u proizvodnji kao velike stavke sa pretragom i zadnja 3 korištena na vrhu,
   operater i mašine zapamćeni (localStorage u try/catch), stepperi 48 px sa `inputmode`, lot samo kad treba,
   `Gotovo` onemogućeno sa objašnjenjem bez završne kontrole, toast "Spremljeno DP-…", greške iznad dugmeta.
2. Mobilni prikaz lista: `DataTable` redovi postaju kartice ispod `sm`; ostale tabele (tabovi) bez horizontalnog skrolanja.
3. CSV export iz `DataTable` (UTF-8 BOM, `;`, decimalni zarez) + test formata.
4. PWA: ručno pisan `manifest.webmanifest` + ikona (PNG generisan bez paketa), bez service workera.
5. Prolaz kroz tekstove u `web/src` (engleski, ekavica, greške bez uputstva) + lista ispravki.
6. Provjera svih ekrana na 360×740 i 375×812 + lista u logu.

### Faza 4 — odluke

- **Radni sati u Brzom unosu.** Pitanje: UI §4 stavlja "Sati, utrošak materijala (opcionalno, sklopljeno)", a server
  traži `LaborHours` (DATA_MODEL: decimal ≥ 0, nije null). Opcije: (a) sati vidljivi i obavezni, sklopljeni samo utrošak
  i napomena; (b) prazni sati → 0 (krši zlatno pravilo 6 "nepoznato ≠ nula"); (c) `LaborHours` nullable (promjena
  modela). Izabrano (a): bez promjene modela i pravila, najmanji obim, lako promijeniti.
  Upisano u UI §4 "(nocna odluka — za potvrdu)".
- **Lot u Brzom unosu** se prikazuje kad je laser > 0 **ili** utrošak > 0 (server traži lot u oba slučaja, DOMAIN §5.2).
  Ako je na nalogu tačno jedan prihvaćen lot, on je unaprijed izabran. Kad nalog nema prihvaćen vezan lot, ekran kaže
  gdje se veže (tab Materijal) — Brzi unos ne nudi lotove koji nisu vezani za nalog (UI §4: "samo lotovi vezani za nalog").
- **Lista naloga u Brzom unosu** = nalozi u statusima iz DOMAIN §5.1 (Spreman, Laser, Presanje, Kontrola, Doraditi).
- **Datum** je uvijek danas (prikazan, ne mijenja se na ovom ekranu); stariji datum ide kroz "Dnevna proizvodnja → Novi".
- Uklonjen `EmptyModulePage` (više nije korišten).
- **Liste na telefonu:** `DataTable` ispod 640 px prikazuje kartice (prva kolona = naslov/link + polja označena
  `mobile: true`, inače sljedeće 3). Tabele unutar stranica (tabovi naloga, lot, kalibracije, korisnici, dashboard)
  dobile su klasu `cards-sm` (CSS) — red postaje kartica, naziv kolone iz `data-label`.
- **CSV export:** dugme "Izvezi CSV" na svih 10 lista; dohvata sve stranice (po 200) sa istim filterima i sortom.
  Format (BOM, `;`, decimalni zarez, CRLF, prazno za nepoznato, zaštita od formula) pokriven testom `lib/csv.test.ts`.
  Brojevi i datumi idu kao brojevi/datumi koje Excel prepoznaje (bez separatora hiljada; datum `12.10.2026`).
  U browseru dugme nisam kliknula (to bi bilo preuzimanje fajla) — provjereno samo testovima.
- **PWA:** `web/public/manifest.webmanifest` (ručno), ikone generisane skriptom `scripts/make-icons.py`
  (samo standardna Python biblioteka), `start_url` = `/brzi-unos`. Bez service workera. U Caddyfile dodan
  `Content-Type: application/manifest+json`.
  Provjera Caddyfile-a kroz lokalni `tds-erp-caddy` image nije uspjela: `docker run` je ostajao u stanju
  "Created" (bind mount putanje sa razmakom). Uklonila sam samo ta dva kontejnera koje sam sama napravila
  (03:00 i 03:03), ništa drugo. Promjena Caddyfile-a je jedna `header` direktiva istog oblika kao postojeće —
  provjeriti pri prvom deployu.
- **Prolaz kroz tekstove (web/src, 716 tekstova pročitano):** engleskih tekstova vidljivih korisniku nema osim jednog.
  Ispravljeno:
  - `EmployeeFormPage` "Prazno = radnik nema login." → "Prazno = radnik nema korisnički nalog i ne prijavljuje se."
  - `UsersPage` "Zaposlenik se veže za nalog u tabu Zaposlenici." (dvosmisleno: nalog = radni nalog) →
    "Korisnički nalog se veže za zaposlenika u tabu Zaposlenici."
  - `UnsavedChanges` "biće izgubljene" → "bit će izgubljene"; `DocumentFormPage` "biće automatski povučena" → "bit će …".
  - `LotDetailPage` "Razlog odbijanja je obavezan." → "Upiši razlog odbijanja." (UI §9: reci šta uraditi).
  - `LotMovements` "Razlog je obavezan." → "Upiši razlog korekcije ili otpisa."
  - `lib/schema` "Neispravan datum." → "Datum nije ispravan. Odaberi datum u kalendaru."
  - `CalibrationSection` ravni navodnici → „Neusklađeno”.
  Namjerno ostavljeno: "Uslovno odobren" (naziv enuma 1:1 iz Notiona, UI §8), "heat broj", "lot", "NC",
  "dashboard" u "NC bez roka se ne prati na dashboardu" (tekst koji je vlasnik doslovno zadao).
  Server: svi FluentValidation validatori imaju bosanske poruke (provjereno skriptom); engleske poruke postoje samo u
  internim iznimkama koje korisnik ne vidi (vraća se opća bosanska poruka).
- **Provjera ekrana na 360×740 i 375×812** (iframe u Chromeu na localhostu; mjereno: širina stranice i svaki element
  sa horizontalnim skrolanjem): Pregled, Brzi unos, Nalozi, detalj naloga (Osnovno, Proizvodnja, Materijal, Kvalitet,
  Istorija), Novi nalog, Dnevna proizvodnja (lista, detalj, novi), Zalihe (artikli, detalj artikla, lotovi, detalj lota,
  prijem), Kvalitet (kontrole, detalj kontrole, NC lista, detalj NC, nova NC), Dobavljači (lista, detalj), Oprema (lista,
  detalj MS-001), Dokumenti (lista, detalj, novi), Podešavanja (zaposlenici, detalj, korisnici) — 56 provjera.
  Jedini nalaz: tabovi detalja naloga su na telefonu skrolali horizontalno → sada se prelamaju. Ponovna provjera OK.
  Vizuelno pregledane kartice liste naloga i taba Kvalitet na 375 px.
- **Brzi unos u browseru (localhost, dev baza):** izbor naloga RN-2026-0001 → operater → M-01 → laser +2 (lot se
  pojavio i sam izabrao jedini prihvaćeni) → sati +1 → Spremi → "Spremljeno DP-20261011-04", nalog/operater/mašina
  ostali, brojevi očišćeni. `Gotovo` onemogućeno sa objašnjenjem (nalog bez završne kontrole).
  < 20 s: procjena po broju dodira (~9 prvi zapis, ~5 sljedeći); stvarno mjerenje u hali nije moguće bez korisnika.
- Usput u Fazi 3: `Combobox` s praznom opcijom je na Enter tokom pretrage birao praznu opciju — ispravljeno (commit 149e66e).

Pravila rada: vlasnikove upute od 11.10.2026. (plan faze na vrh, odluke ovdje, promjene pravila u docs sa
oznakom "(nocna odluka — za potvrdu)").

## Faza 3 — završetak (odluka vlasnika)

- DROP kolona `equipment.last_calibration_date`, `next_calibration_date`, `calibration_certificate_file_id`
  novom migracijom `DropLegacyEquipmentCalibrationColumns` (izričito odobreno samo za ove tri kolone).
- Uklonjene shadow property mapiranja i komentari; IZV polja na DTO-u ostaju (računaju se iz `equipment_calibration`,
  ne čitaju stare kolone).
- Migracioni test sada provjerava prenos (stara migracija) pa nestanak kolona (nova migracija); mjerno sredstvo samo sa
  ručnim sljedećim datumom se ne prenosi.
- Dev seed već pravi kalibracije kroz `EquipmentCalibration.Record` (MS-001 dvije, MS-002 jedna), sve sa sljedećim datumom.
- DATA_MODEL §9 opisuje samo novo stanje.
