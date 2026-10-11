# Izvještaj — 11.10.2026. (noć + nastavak nakon restarta Dockera)

Ukratko: Faze 0–4 su gotove, osim deploya na VPS (nema VPS-a ni domena). U Fazi 5 je urađeno sve što ne traži
VPS, domen, Storage Box ili klijenta. Svi testovi su zeleni. Detaljan tok i sve odluke su u [NOCNI_LOG.md](NOCNI_LOG.md).

## 1. Pregled svih faza

| Faza | Stanje | Šta fali |
|---|---|---|
| 0 — Kostur i deploy | gotovo osim jedne stavke | deploy na VPS (HTTPS, login na pravom domenu) |
| 1 — Nalozi, materijal, proizvodnja | gotovo | — |
| 2 — Dashboard | gotovo (p95 9,8 ms sa 5.000 zapisa) | — |
| 3 — Kvalitet i QMS | gotovo, uključujući tvoju odluku o DROP-u | — |
| 4 — Hala i dorada | gotovo | "< 20 s po zapisu" je procjena, treba mjerenje u hali |
| 5 — Predaja | import, restore test, uputstvo i health urađeni | stvarni podaci klijenta, vanjski uptime ping |

### Commiti od noćas

**Noć (do zaustavljanja zbog Dockera):**
- `47a7ecb`: Faza 3, DROP tri stare kolone kalibracije na opremi (samo te tri, tvoja odluka). Migracija, test
  prenosa podataka i nestanka kolona, DATA_MODEL §9.
- `bc28729`: Brzi unos `/brzi-unos` po UI §4, provjeren u browseru sa stvarnim API-jem.
- `3b928f6`: liste kao kartice na telefonu i CSV export na svih 10 lista.
- `530de43`: PWA manifest i ikone, bez service workera.
- `c82ebc1`: prolaz kroz tekstove.
- `ac30028`: ROADMAP i log (vidi tačku 3).
- `55c2aab`: izvještaj o zaustavljanju.

**Nastavak (danas):**
- `f1b7be0`: radni sati u Brzom unosu ostaju obavezni, a zadnja vrijednost se pamti na uređaju. UI §4 bez oznake.
- `fc45404`: Caddyfile prošao `caddy validate`; plan Faze 5 je u logu.
- `316c777`: `/api/health` (baza + disk, anoniman, bez osjetljivih podataka) i Docker logovi sa rotacijom
  (5 × 10 MB po servisu).
- `169f582`: `deploy/restore-test.sh`. Restore u privremenu bazu i poređenje broja redova; lokalno prošao.
- `2d1f6b0`: [UPUTSTVO.md](UPUTSTVO.md), jedna strana: brzi unos, prijem lota, status naloga.
- `dafd0ce`: količina prijema lota može biti nepoznata, samo za lotove iz Notiona (vidi tačku 2). Mapiranje
  importa je zapisano u `NOTION_SOURCE.md` „Import” prije koda.
- `e9d7ddd`: Notion import `import-notion <folder> --dry-run | --apply`. **Urađeno, ali netestirano sa stvarnim
  podacima.**

Kako radi import:
- Sve ide u jednoj transakciji. Ako postoji ijedna greška, ništa se ne upisuje.
- Dry-run prođe cijeli upis i vrati ga nazad, pa provjeri i ograničenja baze.
- Izvještaj navodi broj reda za svaki nalaz: greške (prazno obavezno polje, nepoznata vrijednost, konflikt, veza
  koja ne postoji), duplikate i upozorenja za podatke koji se namjerno ne uvoze.
- Početno stanje ulazi kao `Korekcija` sa razlogom "Početno stanje iz Notiona".
- Ponovno pokretanje istog exporta ne pravi ništa novo.

## 2. Odluke za tvoju potvrdu

> **Potvrđeno 11.10.2026.:** količina prijema lotova iz Notiona ostaje prazna dok klijent ne odluči
> (DOMAIN §9.9); pravila importa ostaju kako su opisana ispod.

- **Količina prijema lotova iz Notiona** ([DATA_MODEL.md §6](DATA_MODEL.md), "(nocna odluka — za potvrdu)"):
  - Notion lot nema brojčanu količinu prijema, samo tekst "Dimenzije / količina". Početno stanje ide kao `Korekcija`.
  - Zato je `ReceivedQty` za uvezene lotove `null` (UI prikazuje "—"), a ne izmišljen broj ili 0 (zlatno pravilo 6).
  - Prijem u aplikaciji i dalje traži količinu > 0.
  - Nova migracija samo dozvoljava `null` i ništa ne briše. Vraćanje unazad odbija da nepoznato pretvori u 0.
- **Pravila importa** ([NOTION_SOURCE.md „Import”](NOTION_SOURCE.md)):
  - Broj naloga iz Notiona se zadržava. Brojevi formata `RN-YYYY-NNNN` pomjeraju brojač, da novi nalozi ne dobiju
    isti broj.
  - Status naloga se uvozi kakav jeste, bez provjere prelaza.
  - Osoba se traži po punom imenu zaposlenika. Ako je nema, to je greška u obaveznom polju, a upozorenje u
    neobaveznom.
  - Brojevi se primaju samo sa decimalnom tačkom, jer je zarez dvosmislen (`1,234`).
  - Datumi se primaju u tri formata; datum sa vremenom ili raspon datuma je greška.
  - Kad lot ima i relaciju i tekst dobavljača, a razlikuju se, to je konflikt.
  - `Odradjeno`, `Rezervisano`, kvačice kvaliteta i fajlovi se ne uvoze; svaki takav slučaj je upozorenje u izvještaju.
- **Health je `/api/health`**, ne `/health`, jer Caddy na API šalje samo `/api/*`. Prag diska je 1 GiB.
- **Logovi:** rotacija kroz Docker, bez Seriloga (to bi bio novi paket).
- **Caddy provjera:** putanja repoa ima razmak ("tds erp"), pa je Caddyfile poslan u kontejner kroz stdin umjesto
  bind mounta. Prošlo je bez zastoja.
- **Ryuk kontejneri:** restart Dockera ih je već uklonio, pa nisam imala šta brisati. Kontejneri `--rm` iz provjera
  nisu ostali.
- **Iz noći, još nepotvrđeno** (detalji u logu, Faza 4 — odluke):
  - lot u Brzom unosu se prikazuje i kad je samo utrošak > 0;
  - CSV piše brojeve bez separatora hiljada i datum kao `12.10.2026`;
  - noćas sam uklonila dva Caddy kontejnera koje sam sama napravila i koji su zapeli.

## 3. Šta nije urađeno i zašto

- **Test sa stvarnim podacima klijenta** (ROADMAP Faza 5): nema finalnog Notion exporta. Prvi dry-run sa pravim
  exportom će pokazati da li Notion piše datume i brojeve u formatima koje import prima.
- **Vanjski uptime ping:** treba domen.
- **Deploy na VPS**, kao i restore test i `backup.sh` sa pravim backupom na serveru: treba VPS.
- **Slanje backupa na Hetzner Storage Box:** treba Storage Box.
- **Mjerenje "< 20 s po zapisu" u hali:** treba hala i radnik. Do sada je to procjena po broju dodira.
- **Moja greška od noćas:** commit `ac30028` (samo dokumentacija) pushan je dok backend testovi nisu mogli da se
  pokrenu. Danas je sve ponovo pokrenuto i zeleno.
- **Lot sa nepoznatom količinom prijema nije pogledan u browseru.** Prikaz koristi isti helper koji `null`
  pokazuje kao "—" na drugim mjestima. Dev API proces radi sa starijim buildom, pa ga treba restartovati.

## 4. Testovi i build

- **Backend:** `dotnet build` prolazi bez upozorenja i grešaka. `dotnet test` je zelen: **464 testa** (463 prošla,
  1 namjerno preskočen ručni test performansi).
- **Frontend:** `npm run typecheck`, `npm run build` i `npm test` prolaze: **172 testa**.
- **Lokalne provjere:**
  - restore test na dev bazi: 29 tabela, isti broj redova;
  - dry-run importa nad dev bazom: ispravno je prijavio konflikte sa dev seedom i ništa nije upisao;
  - `docker compose config` sa rotacijom logova je validan;
  - `caddy validate` vraća "Valid configuration".
- **Migracija** `MaterialLotReceivedQtyNullable` je primijenjena na dev bazu (pri pokretanju import komande).

## 5. Šta čeka VPS, domen, Storage Box ili klijenta

**VPS:**
- Prvi deploy po `deploy/README.md` (Faza 0, zadnja stavka).
- Cron za `backup.sh`, pa `deploy/restore-test.sh` sa pravim backupom.
- Import na serveru: `docker compose run --rm … api import-notion /import --dry-run`, a tek onda `--apply`
  (README §5b).

**Domen:**
- `TDS_DOMAIN` i HTTPS certifikat (Caddy ga dobija sam).
- Vanjski uptime ping na `https://<domen>/api/health`.

**Hetzner Storage Box:**
- Kopija backupa van servera (`BACKUP_RSYNC_*` u `.env`). Nije testirano jer nema cilja.

**Klijent (Sanel):**
1. Finalni Notion export (CSV), zatim dry-run, ispravke i stvarni test (ROADMAP Faza 5, druga stavka).
2. Spisak zaposlenika sa punim imenima kao u Notionu, unesen prije importa, i korisnici sa rolama.
3. Potvrda opreme M-01…M-04 i strukture mjernih sredstava (DATA_MODEL §9 je pretpostavka).
4. Mjerenje Brzog unosa u hali.
5. Otvorena pitanja iz DOMAIN §9:
   1. Nalozi samo kroz plazmu, varenje ili savijanje: trebaju li statusi `Plazma` / `Varenje`?
   2. Gotovi komadi tek nakon završne kontrole (§5.3): dnevno samo laser/presa i gotovi na kraju, ili djelimična
      završna kontrola po seriji?
   3. Kako se mjeri utrošak materijala (table, kg, ostatak table)?
   4. Postoji li format broja naloga? Import zasad zadržava brojeve iz Notiona.
   5. Treba li polje za smjenu?
   6. Šta iz sekcije "03 · Resursi i QMS" stvarno koriste?
   7. Pragovi "Nisko" / "Ispod minimuma".
   8. Ko su korisnici, koliko ih je, ko je Kontrolor?
   9. Može li dati brojčanu količinu prijema za lotove iz Notiona? Do tada je prazna.
