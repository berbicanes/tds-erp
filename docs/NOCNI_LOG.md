# Noćni log (autonomni rad)

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
