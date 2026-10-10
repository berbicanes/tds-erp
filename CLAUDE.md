# TDS ERP — upute za Claude Code

Interni sistem za praćenje proizvodnje i kvaliteta za **TDS-PRODUCTS** (obrt, metaloprerada,
Sanski Most, BiH). Jedan klijent, jedna radionica, 3–10 korisnika. Nije SaaS, nije multi-tenant.

Radionica: CNC fiber laser 6 kW, CNC plazma 85 A, hidraulična presa 350 t, presa 25 t (Durmark),
varenje/čelične konstrukcije. ISO 9001:2015 certifikovana (TÜV NORD). Kupci u CH, DE i BiH.

> **Status:** projekat još ne postoji. Faza 0 u `docs/ROADMAP.md` ga kreira. Ništa u ovom
> fajlu ne opisuje postojeći kod — opisuje kakav kod mora biti.

## Dokumentacija — pročitaj prije rada na modulu

| Fajl | Kada |
|---|---|
| `docs/DATA_MODEL.md` | **Izvor istine** za entitete, polja, enum vrijednosti, relacije. Prije svake migracije. |
| `docs/DOMAIN.md` | Poslovna pravila: zalihe, statusi naloga, sljedivost, QMS. Prije svakog servisa/validacije. |
| `docs/UI.md` | Ekrani, dashboard, brzi unos u hali, formatiranje. Prije svakog frontend rada. |
| `docs/ROADMAP.md` | Faze i kriteriji završetka. Radi samo fazu koja je na redu. |
| `docs/NOTION_SOURCE.md` | Snimak klijentove Notion šeme (test verzija). Samo za import i poređenje. |

## Zlatna pravila (ne pregovara se)

1. **Zaliha se nikad ne upisuje direktno.** Stanje lota = suma `StockMovement` zapisa.
   Nema `UPDATE ... SET quantity`. Ispravka = novi pokret tipa `Korekcija` sa razlogom.
2. **Izvedene vrijednosti se računaju, ne čuvaju se kao unos.** `Odrađeno` na nalogu,
   `Raspoloživo`, `Nivo zalihe`, "prvi komad potvrđen", "završna kontrola prošla" —
   sve se izvodi iz izvornih zapisa (vidi `DOMAIN.md` §3). Nikad editabilno polje u formi.
3. **Sljedivost je obavezna.** Svaki dnevni zapis proizvodnje veže nalog + lot materijala +
   opremu. Lot se smije koristiti samo ako je `ReceiptDecision = Prihvacen`.
4. **QMS zapisi se ne brišu.** Kontrole, neusaglašenosti, lotovi, dnevni zapisi i dokumenti
   imaju samo soft-delete (`IsVoided` + razlog). Svaka promjena ide u `AuditLog`.
5. **`DATA_MODEL.md` je ugovor.** Ne dodaji polje, enum vrijednost ni tabelu koja nije tamo.
   Ako treba — stani, predloži izmjenu dokumenta, čekaj potvrdu. Prvo dokument, pa migracija.
6. **Nepoznato ≠ nula.** Dashboard i izvještaji prikazuju `—` kad podatka nema. Nikad ne
   izmišljaj, ne procjenjuj i ne popunjavaj prazninu nulom.
7. **UI je na bosanskom (ijekavica, latinica).** Kod, identifikatori, commit poruke na engleskom.
   Mapiranje pojmova je u `DOMAIN.md` §1 — koristi tačno ta imena.

## Stack

- **Backend:** .NET 10 (LTS), ASP.NET Core Minimal API, EF Core 10 + Npgsql, FluentValidation
- **Baza:** PostgreSQL 17
- **Auth:** ASP.NET Core Identity, cookie auth (isti origin, bez JWT), role: `Admin`, `Kontrolor`, `Operater`
- **Frontend:** React 19 + TypeScript (strict) + Vite, TanStack Query, React Router, React Hook Form + Zod, Tailwind CSS, Recharts
- **Fajlovi:** lokalni disk (Docker volume) iza `IFileStorage` interfejsa
- **Deploy:** jedan VPS, Docker Compose (api + web + postgres + caddy), dnevni `pg_dump` van servera
- **Testovi:** xUnit + Testcontainers (pravi Postgres), Vitest za frontend logiku

Ne uvoditi: MediatR, CQRS, mikroservise, Redis, message queue, GraphQL, Clean Architecture
sa 5 projekata, repository pattern iznad EF Core-a. Jednostavno je namjerno.

## Struktura

```
/CLAUDE.md
/docs/                      specifikacija (gore)
/src/Tds.Api/               jedini backend projekat
  Program.cs
  Data/                     AppDbContext, konfiguracije entiteta, Migrations/, AuditInterceptor
  Domain/                   entiteti, enumi, domenska pravila (čisti C#, bez EF/HTTP)
  Features/<Modul>/         endpointi + DTO + validatori po modulu (vertical slice)
    WorkOrders/ ProductionEntries/ Materials/ Stock/ Suppliers/
    QualityChecks/ NonConformities/ Equipment/ Documents/ Dashboard/ Employees/ Files/
  Infrastructure/           IFileStorage, numeracija dokumenata, vrijeme (IClock)
/src/Tds.Api.Tests/         unit (Domain) + integracioni (Testcontainers)
/web/                       React app
  src/features/<modul>/     ekrani, hookovi, zod šeme po modulu
  src/components/           dijeljene UI komponente
  src/lib/                  api klijent, formatiranje (datum, broj), auth
/deploy/                    docker-compose.yml, Caddyfile, backup.sh, .env.example
```

## Komande (kreira ih Faza 0 — provjeri da postoje prije korištenja)

```bash
docker compose -f deploy/docker-compose.dev.yml up -d db   # lokalni Postgres
dotnet run --project src/Tds.Api                            # API na :5080
dotnet test                                                 # svi testovi
dotnet ef migrations add <Ime> -p src/Tds.Api               # nova migracija
cd web && npm run dev                                       # frontend na :5173 (proxy /api → :5080)
cd web && npm run build && npm run typecheck && npm test
```

## Konvencije

- **API:** REST pod `/api/...`, JSON camelCase, greške kao `ProblemDetails` (RFC 9457) sa
  porukom na bosanskom u `detail`. Validacijske greške po polju u `errors`.
- **Vrijeme:** poslovni datumi su `DateOnly` (rok, datum prijema, datum proizvodnje).
  Tehnički timestampi `DateTimeOffset` u UTC (`timestamptz`). "Danas" = `Europe/Sarajevo`,
  uvijek preko `IClock`, nikad `DateTime.Now` u logici.
- **Količine:** `decimal(12,3)`. Komadi su cijeli brojevi (`int`). Nikad `double`.
- **Numeracija** (`RN-2026-0001` itd.) se generiše na serveru u transakciji, po godini.
  Format u `DOMAIN.md` §6.
- **EF Core:** enumi kao string u bazi. Snake_case imena tabela/kolona (`UseSnakeCaseNamingConvention`).
  Svaki entitet: `Id` (Guid v7), `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`.
- **Frontend:** server-state samo kroz TanStack Query. Forme kroz RHF + Zod; Zod šema
  odražava serversku validaciju, ali server je jedini autoritet.
- **Formatiranje u UI:** datum `dd.MM.yyyy.`, decimale zarezom (`1.234,5`), helperi u `web/src/lib/format.ts`.
- **Commit:** mali, jedan logički korak. Poruka na engleskom, imperativ.

## Prije nego kažeš "gotovo"

- `dotnet build` bez upozorenja, `dotnet test` zeleno, `npm run typecheck` i `npm run build` prolaze.
- Svako novo domensko pravilo ima unit test, uključujući negativni slučaj (pravilo odbija).
- Nova migracija pregledana: nema `DROP` kolone sa podacima bez eksplicitne potvrde.
- Ekran provjeren na 375 px širine ako ga koristi hala (`UI.md` §4).
- Ako si odstupio od dokumenta, reci to eksplicitno — ne prešućuj.

## Zabranjeno bez izričitog zahtjeva

- Ponude, kalkulacija cijene, fakture, otpremnice, knjigovodstvo, DXF parsiranje, nesting,
  planiranje mašina (Gantt). To nije u obimu — `ROADMAP.md` "Van obima".
- Brisanje/izmjena postojećih migracija koje su primijenjene.
- Novi NuGet/npm paketi osim onih u Stacku — pitaj prvo.
- Engleski tekst u UI-ju, lorem ipsum, izmišljeni demo podaci u produkcijskom seedu.
- Slanje podataka bilo kojem vanjskom servisu (analitika, CDN fontovi sa trackingom, AI API).
