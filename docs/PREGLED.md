# Pregled koda — 11.10.2026.

Nezavisan pregled backenda (`src/Tds.Api`) i frontenda (`web/src`) prema `CLAUDE.md`, `DATA_MODEL.md` i `DOMAIN.md`.
**Ništa nije popravljeno.** Svaki nalaz je provjeren čitanjem koda; pretpostavke nisu upisane kao nalazi.

Ozbiljnost: **Kritična** (gubitak/iskrivljenje podataka ili zaobilaženje sigurnosti), **Visoka**, **Srednja**,
**Niska** (kvalitet podataka, sitni rizik).

## Sažetak

| Traženo | Rezultat |
|---|---|
| Kršenje zlatnih pravila | 2 nalaza (pravilo 6), 1 napomena o istorijskoj migraciji |
| Zaliha koja se ne računa iz `StockMovement` | nema — svuda Σ neponištenih pokreta |
| Endpointi bez provjere uloge | nema |
| Hard delete | nema (samo veze fajlova, dozvoljeno DOMAIN §7.7) |
| Validacija samo na frontendu | 4 nalaza (zajednički uzrok, vidi N1) |
| SQL bez parametara | nema |
| Fajlovi dostupni bez prijave | nema; 2 sitne napomene |

Kritičnih i visokih nalaza nema.

## Nalazi

### N1 — Srednja · Izostavljeno polje u JSON-u postaje 0 / 0001-01-01 (zajednički uzrok)
Request DTO-ovi koriste ne-nullable tipove (`DateOnly`, `decimal`), a JSON opcije nemaju
`RespectRequiredConstructorParameters`. Polje koje klijent ne pošalje dobije podrazumijevanu vrijednost, a validator
za neka polja nema `NotEmpty`. Frontend uvijek šalje ova polja, pa se problem javlja samo kod direktnog API poziva
(ili buduće greške u frontendu). Pojedinačni slučajevi:

| # | Polje | Lokacija | Šta server prihvati |
|---|---|---|---|
| N1a | `ReceivedDate` pri prijemu lota | `Features/Materials/LotEndpoints.cs` — `ReceiveLotValidator` (≈ red 49) | `POST /api/lots` bez `receivedDate` → lot i `Prijem` pokret sa datumom 0001-01-01 (sljedivost). |
| N1b | `LaborHours` na dnevnom zapisu | `Features/ProductionEntries/ProductionEntryEndpoints.cs:42` (DTO), `:62` (validator) | zapis bez `laborHours` → 0 sati. UI §4 kaže "server ih traži, a prazno ≠ 0" — server ih ne traži (zlatno pravilo 6). |
| N1c | `Date` ručnog pokreta zalihe | `Features/Stock/StockMovementEndpoints.cs:27-36` — `ManualMovementValidator` | korekcija/otpis bez `date` → pokret od 0001-01-01. *(Niska)* |

Frontend lokacije: `web/src/features/stock/ReceiveLotPage.tsx:23`, `production/ProductionEntryFormPage.tsx` i
`quickEntry/quickEntry.ts` (sati), `stock/LotMovements.tsx:72` (datum ima podrazumijevanu vrijednost, bez provjere).

### N2 — Srednja · Lot iz Notiona bez poznatog stanja prikazuje "Na stanju 0" (zlatno pravilo 6)
- `Features/Materials/LotEndpoints.cs:133` (`?? 0m`), `Features/Stock/StockQueries.cs:43` (`COALESCE(…, 0)`).
- Scenarij: Notion red ima praznu "Količina na stanju" → import upozori "stanje je nepoznato" i ne pravi pokret.
  Lot tada u listi i na detalju (`web/src/features/stock/LotsPage.tsx:19`, `LotDetailPage.tsx:177`) pokazuje 0, a u
  stanju artikla ulazi kao 0 (može dati "Nema" / "Ispod minimuma" na dashboardu). Za sve ostale lotove 0 pokreta
  zaista znači 0; ovo je jedini slučaj gdje znači "nepoznato". Nastalo s Fazom 5 (nullable `ReceivedQty`).

### N3 — Niska · Datum odobrenja dokumenta u budućnosti na nacrtu
- `Features/Documents/DocumentEndpoints.cs` — `SaveDocumentValidator` / `Apply` ne provjeravaju da `ApprovalDate`
  nije u budućnosti; provjera postoji samo pri prelazu u `Odobren` (`DocumentTransitions.Check`). Frontend ima samo
  `max` na date pickeru (`web/src/features/documents/DocumentFormPage.tsx:254`).
- Posljedica: nacrt može čuvati budući datum odobrenja; odobravanje je i dalje blokirano. Samo kvalitet podataka.

### N4 — Niska · Prijava: provjera lozinke deaktiviranog naloga bez zaključavanja
- `Features/Auth/AuthEndpoints.cs:36-39`: za deaktiviran nalog `CheckPasswordAsync` se zove prije
  `PasswordSignInAsync`, bez brojanja neuspjelih pokušaja. Poruka "Nalog je deaktiviran" se vraća samo uz tačnu
  lozinku → neograničeno pogađanje lozinke deaktiviranog naloga, sa potvrdom kad je tačna (rizik ako ista lozinka
  postoji drugdje).
- Uz to: poruka o zaključavanju (`:43-45`) se pojavljuje samo za postojeća korisnička imena, pa otkriva da nalog
  postoji. Za internu aplikaciju sa 3–10 korisnika mali rizik.

### N5 — Niska · Istorijska migracija kalibracija (već primijenjena, samo napomena)
- `Data/Migrations/20261011000114_EquipmentCalibrations.cs:66`: preneseni redovi dobijaju `result = 'Uskladjeno'`
  (rezultat koji niko nije upisao) i nemaju AuditLog zapis (raw SQL zaobilazi `AuditInterceptor`).
  `performed_by` je pošteno "Nije navedeno". Mjerna sredstva sa samo ručnim sljedećim datumom nisu prenesena — to je
  bila tvoja odluka, i DROP je obrisao te datume.
- Migracija je primijenjena i ne mijenja se; ispravka bi bila nova korekcija podataka, ako uopšte treba (na dev bazi
  nema produkcijskih podataka).

### N6 — Niska · Fajlovi: keš u browseru i nevezani fajlovi
- `Features/Files/FileEndpoints.cs` (`FileResultWithDisposition`): `Cache-Control: private, max-age=3600` — nakon
  odjave fajl ostaje sat vremena u kešu browsera; na dijeljenom telefonu u hali sljedeći korisnik ga može otvoriti
  iz istorije. `no-store` bi to spriječio.
- Upload (`POST /api/files`) pravi fajl i prije vezivanja za zapis; nevezan fajl ostaje zauvijek i dostupan je svakom
  prijavljenom korisniku koji zna Id. Nije curenje van aplikacije.

## Provjereno i čisto

- **Uloge:** svaki `/api` endpoint ima `RequireAuthorization` (čitanje: svi prijavljeni; pisanje: `KontrolorOrAdmin`;
  poništavanje, uklanjanje veza fajlova, korisnici, zaposlenici: `Admin`), u skladu sa DOMAIN §8. Anonimni su samo
  `/api/auth/login`, `/api/auth/logout` i `/api/health`. Dnevni zapis: autor samo isti dan, Kontrolor/Admin uvijek
  (`ProductionEntryEndpoints.CheckRights`). Admin ne može sebi oduzeti ulogu ni deaktivirati se.
- **Zaliha:** `StockQueries` (jedan SQL, Σ neponištenih pokreta, rezervacije samo za aktivne naloge),
  `StockLocks.OnHandAsync`, `LotEndpoints.Project`. Nema kolone stanja ni `UPDATE` količine. Sve izmjene stanja
  (korekcija, otpis, odluka "Odbijen", dnevni zapis sa utroškom) zaključavaju lot (`SELECT … FOR UPDATE`) u transakciji.
- **Hard delete:** `AuditInterceptor` baca grešku na brisanje auditovanog entiteta; nema `ExecuteDelete`/`ExecuteUpdate`.
  Jedina brisanja su veze fajlova (dobavljač, dokazi KK/NC) — samo Admin, bilježi se u AuditLog, fajl ostaje (DOMAIN §7.7)
  — i opreme na dnevnom zapisu pri izmjeni (auditovano na zapisu).
- **SQL:** `Infrastructure/Sql.cs` uvijek veže parametre; `$"""` u `StockQueries` umeće samo konstantu `LotCte`;
  `FromSql`/`ExecuteSqlInterpolated` su parametrizovani; sortiranje ide kroz whitelist (`SortMap`); pretraga
  (`ILike`) escapuje `%`, `_`, `\`.
- **Fajlovi:** download traži prijavu; Caddy servira samo `web/dist` (`deploy/caddy.Dockerfile`), fajlovi su u API
  kontejneru; putanja na disku je Guid i `Resolve` odbija izlazak iz storage roota; sadržaj se provjerava po
  ekstenziji; `nosniff`.
- **Zlatna pravila 2, 3, 4, 5, 7:** nijedan request DTO ne prima izvedene vrijednosti; lot obavezan i `Prihvacen` pri
  rezanju/utrošku, mašina obavezna; QMS zapisi se samo poništavaju sa razlogom; tabele, kolone i enumi u snapshotu
  odgovaraju DATA_MODEL-u; u UI-ju nema engleskih tekstova (provjera uzorkom).
- **GET ne mijenja stanje:** nijedan GET ne piše.
- **Poništeni zapisi:** svi zbirovi i izvedene oznake filtriraju `!IsVoided`.
- **Ostale validacije** (korisnici, zaposlenici, dobavljači, NC, kontrole, nalozi, dnevni zapisi, artikli, oprema i
  kalibracije, status naloga, odluka i poništavanje lota): sva frontend pravila postoje i na serveru.
