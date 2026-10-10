# ROADMAP

Radi se jedna faza po jedna. Faza je gotova kad su **svi** kriteriji označeni.
Ne počinjati sljedeću fazu dok prethodna nije gotova, osim ako vlasnik projekta kaže drugačije.

Interni cilj: 2 sedmice intenzivnog rada. Klijentu komunicirano: 4 sedmice.

---

## Faza 0 — Kostur i deploy (dan 1–2)

- [x] Solution: `src/Tds.Api`, `src/Tds.Api.Tests`, `web/` po strukturi iz `CLAUDE.md`
- [x] `deploy/docker-compose.dev.yml` sa Postgres 17; `dotnet run` se spaja
- [x] EF Core + Npgsql + snake_case konvencija; `AuditInterceptor`; bazni entitet sa audit poljima
- [x] `IClock` (Europe/Sarajevo), `IFileStorage` (lokalni disk), numeracija (`number_sequence`)
- [x] Identity sa cookie auth, 3 role, seed Admin korisnika iz env varijabli (ne hardkodirano)
- [x] Login / logout ekran, zaštićene rute, `GET /api/me`
- [x] Vite + React + TS strict + Tailwind + TanStack Query + Router; proxy `/api`
- [x] `format.ts` (datum, broj) i `labels.ts` (enum → tekst, boja) sa testovima
- [x] Layout: sidebar + mobilna donja traka, prazne stranice za sve module
- [x] Testcontainers integracioni test koji pokreće migracije i radi login
- [x] Produkcijski `docker-compose.yml` + Caddy (HTTPS) + `backup.sh` (pg_dump + fajlovi, rotacija 30 dana)
      > Provjereno lokalno (11.10.2026.): HTTPS na `localhost` (Caddy interni certifikat), login,
      > SPA fallback, sesija preživi restart; `backup.sh` bez remote cilja (dump, snapshot fajlova,
      > rotacija, upozorenje). Slanje na Hetzner Storage Box **nije** testirano — nema cilja.
- [ ] Deploy na VPS radi, HTTPS radi, login radi

> Napomena (10.10.2026.): VPS i domen još ne postoje. Sve za deploy se priprema
> (`deploy/README.md`), a ova stavka ostaje neoznačena dok deploy stvarno ne proradi.
> To **nije** blokada za Fazu 1.

## Faza 1 — Jezgro: nalozi, materijal, proizvodnja (dan 3–6)

- [x] Employee CRUD (Admin)
- [x] Equipment CRUD + seed M-01…M-04
- [x] Supplier CRUD (bez fajlova za sad je OK, fajlovi u Fazi 3)
- [x] MaterialItem CRUD
- [x] MaterialLot: kreiranje generiše `Prijem`; odluka prijema; pravila DOMAIN §2.5, §7.2
- [x] StockMovement: korekcija i otpis (Kontrolor/Admin), lista pokreta po lotu
- [x] Upiti stanja: OnHand, Reserved, Available po lotu i artiklu; Nivo zalihe (DOMAIN §2.4)
- [x] QualityCheck (DATA_MODEL §10) — **osnova već u Fazi 1** da gate-ovi statusa (DOMAIN §4) rade
      nad pravim podacima: tabela, migracija, numeracija `KK`, endpointi za kreiranje i listu po nalogu,
      izvedene oznake prvog komada i završne kontrole na nalogu; na detalju naloga samo jednostavno
      dodavanje kontrole. Puni ekrani kontrola su u Fazi 3.
- [x] WorkOrder CRUD + upload crteža + numeracija
- [x] WorkOrderMaterial: vezivanje lota i rezervacija
- [x] Prelazi statusa sa svim gate-ovima (DOMAIN §4) — **unit test za svaki red tabele prelaza**
- [x] ProductionEntry: sva pravila DOMAIN §5, automatsko `Izdavanje`, poništavanje
- [x] Izvedene vrijednosti naloga (DOMAIN §3) u DTO-u
- [x] Liste + detalj naloga (UI §5, §6) bez taba Kvalitet
- [x] Integracioni test: lot → nalog → rezervacija → dnevni zapis sa utroškom → stanje tačno

> Napomene (11.10.2026.):
> - Uz zaposlenike je urađeno i upravljanje korisničkim nalozima (Admin), dogovoreno prije Faze 1.
> - "Liste + detalj": sortiranje, pretraga (300 ms), filteri u URL-u, paginacija 50 — urađeno. Mobilne
>   kartice i CSV export su u Fazi 4 (na mobilnom tabele skroluju unutar svog okvira, stranica ne).
> - Detalj naloga ima jednostavan tab Kvalitet (dodavanje kontrole), po odluci da `QualityCheck` ide u Fazu 1.
>   Tab Istorija dolazi u Fazi 3.

## Faza 2 — Dashboard (dan 7–8)

- [x] `GET /api/dashboard` — jedan upit po sekciji, ukupno < 200 ms sa 5.000 dnevnih zapisa (seed za test)
      > Mjereno 12.10.2026. (`scripts/perf-dashboard.sh`, Release, API u procesu, Postgres 17 u Dockeru,
      > MacBook Air): 5.000 dnevnih zapisa, 2.150 pokreta zalihe, 300 naloga (45 aktivnih), 150 lotova,
      > 40 artikala, 600 kontrola, 8.582 reda AuditLog-a — 30 poziva nakon 5 zagrijavanja:
      > **p50 = 8,3 ms, p95 = 9,8 ms, max = 12,6 ms**. 7 SQL upita po pozivu (jedan po sekciji, test
      > `One_query_per_section`). Vrijeme mreže i Caddyja nije uključeno.
- [x] KPI pločice, Danas u proizvodnji + graf 14 dana, Nalozi po roku, Zalihe, Bilješke (UI §3)
- [x] Svaki broj vodi na filtriranu listu (filter u URL-u)
- [x] Prazna stanja i `—` za nepostojeće podatke
- [x] Upozorenja sekcija (za sad samo iz Faze 1 podataka; QMS rokovi se dodaju u Fazi 3)
      > Faza 2: lotovi Na kontroli/Karantin, `Blokirano` nalozi, rokovi ocjene dobavljača, kalibracije i
      > popisa (≤ 30 dana ili prošli), mjerna sredstva van kalibracije. NC i dokumenti dolaze u Fazi 3.

## Faza 3 — Kvalitet i QMS (dan 9–10)

- [ ] QualityCheck: puni ekrani (lista, filteri, uređivanje, poništavanje, dokazi); osnova i izvedene oznake su iz Faze 1
- [ ] Neuspjela kontrola nudi kreiranje NC (DOMAIN §7.1)
- [ ] NonConformity sa prelazima (DOMAIN §7.3)
- [ ] Mjerna sredstva + kalibracija (DOMAIN §7.5)
- [ ] ControlledDocument sa verzijama (DOMAIN §7.4)
- [ ] Fajlovi na svim QMS entitetima (certifikati, dokazi)
- [ ] Tab Kvalitet na detalju naloga, tab Istorija (AuditLog)
- [ ] Dashboard upozorenja: svi rokovi iz DOMAIN §7.6

## Faza 4 — Hala i dorada (dan 11–12)

- [ ] Brzi unos (UI §4), testiran na 360 i 375 px, < 20 s po zapisu
- [ ] PWA manifest + ikona (dodavanje na početni ekran telefona); bez offline režima
- [ ] Mobilni prikaz svih lista (kartice)
- [ ] CSV export lista
- [ ] Prolaz kroz sve tekstove: bosanski, ijekavica, bez engleskih ostataka

## Faza 5 — Predaja (dan 13–14)

- [ ] Import iz Notion CSV exporta (nalozi, lotovi, dobavljači, zalihe → početna `Korekcija`)
      — jednokratna admin komanda, ne UI. Prvo dry-run sa izvještajem.
- [ ] Test sa stvarnim podacima klijenta, ispravke
- [ ] Restore test backupa na čistu bazu — mora raditi
- [ ] Kratko uputstvo za korisnike (1 strana): brzi unos, prijem lota, status naloga
- [ ] Monitoring: healthcheck endpoint + vanjski uptime ping; greške u log fajl sa rotacijom

---

## Van obima (ne raditi bez novog dogovora)

Ponude i kalkulacija cijene · DXF/STEP parsiranje · nesting · fakture i otpremnice ·
knjigovodstvo · planiranje mašina / Gantt · registar kupaca · offline rad · nativna
mobilna aplikacija · tamna tema · više jezika · integracija sa CAM softverom lasera ·
kompetencije, ciljevi, rizici, auditi (QMS sekcija 03) dok klijent ne potvrdi.
