# DATA_MODEL — izvor istine

Izvedeno iz klijentove Notion šeme "Proizvodnja i QMS" (snimak 10.10.2026, vidi
`NOTION_SOURCE.md`). Klijent pravi finalnu Notion verziju — kad stigne, **prvo se ažurira
ovaj dokument**, tek onda migracije.

Oznake u koloni "Izvor":
- `N` — polje postoji u Notionu 1:1
- `N*` — postoji u Notionu, ali je promijenjeno (razlog u napomeni)
- `NOVO` — ne postoji u Notionu, dodano jer bez njega pravila iz `DOMAIN.md` ne rade
- `IZV` — izvedeno, ne čuva se kao unos (računa se u upitu ili servisu)

Svi entiteti imaju i: `Id` (Guid v7), `CreatedAt`, `CreatedBy`, `UpdatedAt`, `UpdatedBy`.
QMS entiteti (označeni 🔒) imaju i `IsVoided`, `VoidedReason`, `VoidedAt`, `VoidedBy`.

- `CreatedBy`, `UpdatedBy`, `VoidedBy` su `Guid?` = Id korisnika (`AspNetUsers`, `IdentityUser<Guid>`).
  `null` = sistemska akcija (seed, import, migracija). Ne postoji "system" korisnik. UI prikazuje
  `null` kao "Sistem".
- `CreatedAt`, `UpdatedAt`, `VoidedAt` su `DateTimeOffset` (UTC, `timestamptz`). Pri kreiranju
  `UpdatedAt`/`UpdatedBy` = `CreatedAt`/`CreatedBy`.
- `VoidedReason` je string(300), obavezan pri poništavanju.
- **Izuzeci** (nemaju audit polja i ne audituju se): Identity tabele (`AspNetUsers`, role…),
  `number_sequence` (§16) i `AuditLog` (§15). `AuditLog` ima samo `Id` (Guid v7) i svoja polja.
- Audituje se (AuditLog) sve što nasljeđuje bazni entitet.

Korisnički nalog (Identity): login korisničkim imenom (`UserName`), `Email` opcionalan.

---

## 1. Employee — Zaposlenik

Notion "person" polja (Operater, Kontrolisao, Odgovorna osoba, Vlasnik radnje, Vlasnik
dokumenta) su Notion korisnici. Radnici u hali nemaju nužno nalog, zato je zaposlenik
odvojen od korisničkog naloga.

| Polje | Tip | Izvor | Napomena |
|---|---|---|---|
| FullName | string(120), req | NOVO | |
| Position | string(80)? | NOVO | npr. "Operater lasera" |
| IsActive | bool | NOVO | neaktivni se ne nude u izborima, ostaju u istoriji |
| UserId | FK → AspNetUsers? | NOVO | null = nema login |

## 2. WorkOrder — Nalog proizvodnje (`Nalozi proizvodnje`)

| Polje | Tip | Izvor | Napomena |
|---|---|---|---|
| Number | string(20), unique | N (`Broj naloga`, title) | auto, `RN-YYYY-NNNN` |
| Customer | string(150), req | N (`Kupac`, text) | slobodan tekst, NE tabela kupaca (Sanel nema registar kupaca) |
| ProductDescription | string(300), req | N (`Proizvod / opis`) | |
| OrderedQty | int > 0, req | N* | Notion ima **dva** polja: `Kolicina Narudzbe` i `Naručena količina` — duplikat, spojeno u jedno |
| DueDate | DateOnly, req | N (`Rok isporuke`) | |
| ReleaseDate | DateOnly? | N (`Datum puštanja`) | puštanje u proizvodnju |
| Status | enum `WorkOrderStatus` | N (`Status`) | vrijednosti u "Enumi" ispod; prelazi u `DOMAIN.md` §4 |
| Priority | enum `Priority` | N (`Prioritet`) | `Normalan`, `Hitno` |
| QualityApproval | enum `QualityApproval` | N (`Odobrenje kvaliteta`) | `Ceka`, `Prihvaceno`, `Blokirano` — postavlja samo Kontrolor/Admin |
| DrawingNumber | string(60)? | N (`Broj crteža`) | |
| DrawingRevision | string(20)? | N (`Revizija crteža`) | |
| DrawingFileId | FK → FileAttachment? | N (`Važeći crtež`, file) | |
| ResponsibleEmployeeId | FK → Employee? | N (`Odgovorna osoba`) | |
| Notes | string(2000)? | NOVO | |
| CompletedQty | int | **IZV** (`Odradjeno`) | Σ `ProductionEntry.GoodQty` (neponišteni). U Notionu ručni broj — ovdje se računa. |
| LaserFirstPieceApproved | bool | **IZV** | postoji `QualityCheck` faze `LaserskoRezanje`, rezultat `Prihvaceno` |
| PressFirstPieceApproved | bool | **IZV** | isto, faza `HidraulicnoPresanje` |
| FinalInspectionPassed | bool | **IZV** | isto, faza `ZavrsnaKontrola` |
| ReadyForShipment | bool | **IZV** | `FinalInspectionPassed && QualityApproval == Prihvaceno && CompletedQty >= OrderedQty` |
| IsOverdue | bool | **IZV** | `DueDate < danas && Status ∉ {Zavrseno, Isporuceno, Otkazano}` |

Uklonjeno iz Notiona: `Lot materijala` (text) — duplikat relacije `Materijalni lotovi`.

Relacije:
- `Materijalni lotovi` → **WorkOrderMaterial** (M:N sa količinom rezervacije, §8)
- `Neusaglašenosti / CAPA` → NonConformity.WorkOrderId (1:N)

## 3. ProductionEntry — Dnevni zapis proizvodnje 🔒 (`Dnevna proizvodnja komada`)

| Polje | Tip | Izvor | Napomena |
|---|---|---|---|
| Number | string(24), unique | N (`Zapis proizvodnje`, title) | auto, `DP-YYYYMMDD-NN` |
| Date | DateOnly, req | N (`Datum`) | default danas, ne u budućnosti |
| WorkOrderId | FK → WorkOrder, req | N (`Radni nalog`, relation) | |
| OperatorEmployeeId | FK → Employee, req | N (`Operater`) | |
| MaterialLotId | FK → MaterialLot? | N (`Korišteni materijalni lot`) | obavezan ako LaserCutQty > 0 ili MaterialConsumed > 0 (vidi DOMAIN §5) |
| EquipmentIds | M:N → Equipment | N (`Oprema korištena`) | najmanje jedna; samo aktivne mašine (`Kind = Masina`); spojna tabela `production_entry_equipment` |
| LaserCutQty | int ≥ 0 | N (`Laser — izrezani komadi`) | |
| PressedQty | int ≥ 0 | N (`Presanje — obrađeni komadi`) | |
| GoodQty | int ≥ 0 | N (`Gotovi ispravni komadi`) | pravilo DOMAIN §5.3 |
| ScrapQty | int ≥ 0 | N (`Škart / za doradu`) | |
| LaborHours | decimal(5,2) ≥ 0 | N (`Radni sati`) | max 24 po zapisu |
| MaterialConsumed | decimal(12,3)? | **NOVO** | utrošak iz lota u jedinici lota. Bez ovoga zaliha se ne može automatski skidati. Ako je prazno → nema pokreta zalihe. |
| Note | string(1000)? | N (`Napomena`) | |

Uklonjeno iz Notiona: `Broj naloga` (text), `Narudzba` (text), `Lot materijala` (text) —
sve duplikati relacija.

Barem jedno od `LaserCutQty`, `PressedQty`, `GoodQty`, `ScrapQty` mora biti > 0.

## 4. Supplier — Dobavljač (`Dobavljači`)

| Polje | Tip | Izvor | Napomena |
|---|---|---|---|
| Name | string(150), unique, req | N (`Naziv dobavljača`) | |
| MaterialOrService | string(300)? | N (`Materijal / usluga`) | |
| ApprovalStatus | enum `SupplierApprovalStatus` | N (`Status odobrenja`) | `UProcjeni`, `Odobren`, `UslovnoOdobren`, `Blokiran` |
| InitialEvaluationDate | DateOnly? | N | |
| NextEvaluationDate | DateOnly? | N (`Sljedeća ocjena`) | ide na dashboard ako ≤ 30 dana |
| EvaluationCriteria | string(2000)? | N (`Kriteriji / rezultat ocjene`) | |
| ResponsibleEmployeeId | FK → Employee? | N | |
| Notes | string(2000)? | N (`Napomene`) | |
| Certificates | M:N → FileAttachment | N (`Certifikati / dokazi`) | spojna tabela `supplier_certificate` |

Relacija `Isporuke i lotovi` = MaterialLot.SupplierId (1:N).

## 5. MaterialItem — Artikal materijala (dio Notion `Zalihe materijala`)

Notion `Zalihe materijala` miješa dvije stvari: **artikal** (vrsta + kvalitet + debljina, sa
minimalnom zalihom) i **stanje po lotu**. Ovdje su razdvojeni.

| Polje | Tip | Izvor | Napomena |
|---|---|---|---|
| Name | string(120), req | N (`Materijal / artikal`, title) | npr. "Lim crni" |
| Grade | string(40)? | N (`Kvalitet / oznaka`) | npr. `S235`, `1.4301`, `AlMg3` |
| ThicknessMm | decimal(6,2)? | N (`Debljina mm`) | |
| Unit | enum `Unit` | N (`Jedinica`) | `kg`, `kom`, `m`, `tabla`, `drugo` |
| MinimumStock | decimal(12,3)? | N (`Minimalna zaliha`) | po artiklu, ne po lotu |
| NextStocktakeDate | DateOnly? | N (`Datum sljedećeg popisa`) | |
| IsActive | bool | NOVO | |

Unique: (`Name`, `Grade`, `ThicknessMm`).

`Unit` se ne može promijeniti čim artikal ima ijedan lot (i poništen) — sve količine lotova i pokreta
su u toj jedinici.

Izvedeno po artiklu (IZV, vidi DOMAIN §2.4):
- `OnHand`, `Reserved`, `Available`, `StockLevel` — samo nad lotovima sa odlukom `Prihvacen`
- `OnHoldQty` ("Na kontroli / karantin") — Σ OnHand lotova sa odlukom `NaKontroli` ili `Karantin`,
  da materijal koji fizički stoji u hali ne bude nevidljiv. Ne ulazi u `Available` ni `StockLevel`.

## 6. MaterialLot — Ulazni lot 🔒 (`Ulazni materijal u proizvodnji`)

| Polje | Tip | Izvor | Napomena |
|---|---|---|---|
| LotNumber | string(60), unique, req | N (`Oznaka lota / heat broj`, title) | upisuje se heat broj sa certifikata |
| MaterialItemId | FK → MaterialItem, req | N* | Notion ima `Vrsta materijala`, `Kvalitet / oznaka`, `Debljina mm` kao tekst/broj na lotu — ovdje kroz artikal |
| SupplierId | FK → Supplier, req | N* (`Dobavljač u registru`) | Notion ima i text `Dobavljač` — duplikat, uklonjeno |
| ReceivedDate | DateOnly, req | N (`Datum prijema`) | |
| DimensionsDescription | string(200)? | N (`Dimenzije / količina`) | npr. "3000×1500, 10 tabli" — opis |
| ReceivedQty | decimal(12,3) > 0, req | **NOVO** | brojčana količina prijema u jedinici artikla; generiše `Prijem` pokret; ne mijenja se nakon kreiranja (ispravka = `Korekcija`) |
| ReceiptDecision | enum `ReceiptDecision` | N (`Odluka prijema`) | `NaKontroli`, `Prihvacen`, `Karantin`, `Odbijen` |
| InspectedByEmployeeId | FK → Employee? | N (`Kontrolisao`) | obavezno kad odluka ≠ `NaKontroli` |
| StorageLocation | string(60)? | N (`Skladišna lokacija`) | |
| CertificateFileId | FK → FileAttachment? | N (`Certifikat materijala`) | upozorenje (ne blokada) ako `Prihvacen` bez certifikata |

Uklonjeno: `Broj naloga` (text) — veza ide kroz WorkOrderMaterial i ProductionEntry.
Relacije `Dnevni zapisi proizvodnje` i `Radni nalozi` su obrnute strane tih veza.

**Nepromjenjivo nakon kreiranja:** `MaterialItemId`, `ReceivedDate`, `ReceivedQty`. Mijenjaju se samo
`LotNumber`, `SupplierId`, `DimensionsDescription`, `StorageLocation` (odluka prijema i certifikat kroz
posebne akcije). Pogrešna količina → `Korekcija`. Pogrešan artikal ili datum prijema → lot se poništi
(Admin, samo kad nema drugih pokreta osim `Prijem` i nije vezan ni za jedan nalog) i unese novi.

## 7. StockMovement — Pokret zalihe 🔒 (NOVO, zamjenjuje ručne brojeve)

| Polje | Tip | Napomena |
|---|---|---|
| MaterialLotId | FK, req | |
| Type | enum `StockMovementType` | `Prijem`, `Izdavanje`, `Korekcija`, `Otpis` |
| Quantity | decimal(12,3), req | predznak: Prijem +, Izdavanje −, Otpis −, Korekcija ± |
| Date | DateOnly, req | |
| ProductionEntryId | FK? | postavljeno kad pokret dolazi iz dnevnog zapisa |
| WorkOrderId | FK? | |
| Reason | string(300)? | obavezan za `Korekcija` i `Otpis` |

Zamjenjuje Notion polje `Količina na stanju` (ručni broj).

## 8. WorkOrderMaterial — Rezervacija lota za nalog (NOVO, zamjenjuje `Rezervisano`)

| Polje | Tip | Napomena |
|---|---|---|
| WorkOrderId | FK, req | |
| MaterialLotId | FK, req | |
| ReservedQty | decimal(12,3) ≥ 0 | 0 = samo veza (sljedivost), bez rezervacije |

Unique (`WorkOrderId`, `MaterialLotId`). Zamjenjuje Notion `Rezervisano` (ručni broj na
zalihi) i relaciju `Materijalni lotovi`.

## 9. Equipment — Oprema i mjerna sredstva (NOVO u dostupnoj šemi)

Notion relacije `Oprema korištena` i `Mjerno sredstvo` pokazuju na baze u sekciji
"03 · Resursi i QMS" kojima nemamo pristup. Struktura je **pretpostavka** — provjeriti
kad stigne finalni Notion.

| Polje | Tip | Napomena |
|---|---|---|
| Code | string(20), unique | `M-01`… za mašine, `MS-001`… za mjerna sredstva |
| Name | string(120), req | |
| Kind | enum `EquipmentKind` | `Masina`, `MjernoSredstvo` |
| Manufacturer | string(80)? | |
| SerialNumber | string(60)? | |
| CalibrationIntervalMonths | int? | samo za mjerna sredstva; predlaže `NextCalibrationDate` nove kalibracije |
| LastCalibrationDate | DateOnly? | **IZV** — `CalibrationDate` zadnje neponištene `EquipmentCalibration` |
| NextCalibrationDate | DateOnly? | **IZV** — `NextCalibrationDate` zadnje neponištene `EquipmentCalibration` |
| CalibrationStatus | enum `CalibrationStatusAtCheck` | **IZV** (samo mjerna sredstva): bez neponištenih kalibracija ili bez `NextCalibrationDate` → `BezDatumaKalibracije`; zadnja kalibracija `Neuskladjeno` ili `NextCalibrationDate < danas` → `VanKalibracije`; inače `UKalibraciji` |
| IsActive | bool | |

Oprema nema kolona za datume kalibracije ni certifikat: sve je u `EquipmentCalibration` (§9a), a gornja
**IZV** polja se računaju pri čitanju. Certifikat kalibracije je na samoj kalibraciji (`CertificateFileId`).

Seed (sa tdsproducts.com, potvrditi sa klijentom):
`M-01` CNC fiber laser 6 kW · `M-02` CNC plazma 85 A · `M-03` Hidraulična 4-stubna presa 350 t ·
`M-04` Hidraulična presa 25 t (Durmark).

## 9a. EquipmentCalibration — Kalibracija mjernog sredstva 🔒 (NOVO, Faza 3)

Istorija kalibracija kao jedna čitljiva lista ("pokaži istoriju kalibracije za MS-003").

| Polje | Tip | Napomena |
|---|---|---|
| EquipmentId | FK → Equipment, req | samo `Kind = MjernoSredstvo` |
| CalibrationDate | DateOnly, req | ne u budućnosti |
| NextCalibrationDate | DateOnly? | predlaže se `CalibrationDate + CalibrationIntervalMonths`; može se promijeniti ručno; ne prije `CalibrationDate` |
| PerformedBy | string(150), req | ko je kalibrisao, npr. vanjska laboratorija |
| Result | enum `CalibrationResult` | `Uskladjeno`, `Neuskladjeno` |
| CertificateFileId | FK → FileAttachment? | certifikat o kalibraciji |
| Note | string(1000)? | |

Ne uređuje se; pogrešan unos se poništi (Admin, razlog) i unese novi.
`Equipment.Kind` se ne može promijeniti čim oprema ima ijednu kalibraciju.

## 10. QualityCheck — Kontrola kvaliteta 🔒 (`Kontrole kvaliteta`)

| Polje | Tip | Izvor | Napomena |
|---|---|---|---|
| Number | string(20), unique | N (`Zapis kontrole`, title) | auto, `KK-YYYY-NNNN` |
| WorkOrderId | FK? | N (`Radni nalog`) | obavezan za faze na nalogu; za `UlazniMaterijal` mora biti prazan (veže lot, ne nalog) |
| MaterialLotId | FK? | N* (`Lot materijala`, text → relacija) | obavezan za fazu `UlazniMaterijal`; opcionalan za ostale faze |
| Phase | enum `InspectionPhase` | N (`Faza`) | `UlazniMaterijal`, `LaserskoRezanje`, `HidraulicnoPresanje`, `ZavrsnaKontrola` |
| Characteristic | string(300), req | N (`Karakteristika / zahtjev crteža`) | |
| AcceptanceCriterion | string(300)? | N (`Kriterij prihvatanja`) | |
| MeasuredResult | string(300)? | N (`Izmjereni rezultat`) | tekst (može biti "48,95 mm" ili "OK vizuelno") |
| SampleSize | int? ≥ 1 | N (`Veličina uzorka`) | |
| Result | enum `InspectionResult` | N (`Rezultat`) | `Ceka`, `Prihvaceno`, `Neuskladjeno` |
| InspectedByEmployeeId | FK → Employee, req | N (`Kontrolisao`) | |
| InspectionDate | DateOnly, req | N (`Datum kontrole`) | |
| MeasuringEquipmentId | FK → Equipment? | N* | Notion ima i text `Mjerna oprema / ID` i relaciju — spojeno u relaciju (samo aktivno, `Kind = MjernoSredstvo`) |
| CalibrationStatusAtCheck | enum `CalibrationStatusAtCheck` | **NOVO** | stanje mjernog sredstva **u trenutku kontrole** (`BezMjernogSredstva` kad ga nema). Postavlja server pri kreiranju, nikad se ne mijenja (trajna oznaka, DOMAIN §7.5) |
| EvidenceFiles | M:N → FileAttachment | N (`Dokaz / zapis`) | spojna tabela `quality_check_evidence` |

Uklonjeno: `Broj naloga` (text) — duplikat relacije.
Kontrola se ne uređuje (DOMAIN §7.0); na postojeću se mogu samo dodati dokazi.

## 11. NonConformity — Neusaglašenost i korektivna radnja 🔒

| Polje | Tip | Izvor | Napomena |
|---|---|---|---|
| Number | string(20), unique | N (`NC broj`, title) | auto, `NC-YYYY-NNN` |
| WorkOrderId | FK? | N (`Radni nalog`) | |
| MaterialLotId | FK? | NOVO | za izvor `Dobavljac` / `UlazniMaterijal` |
| SupplierId | FK? | NOVO | za izvor `Dobavljac` |
| QualityCheckId | FK? | NOVO | kad NC nastaje iz neuspjele kontrole; najviše jedna neponištena NC po kontroli |
| Source | enum `NcSource` | N (`Izvor`) | `Dobavljac`, `UlazniMaterijal`, `Laser`, `Presanje`, `ZavrsnaKontrola`, `Kupac`, `Audit`, `Ostalo` |
| Description | string(2000), req | N (`Opis neusaglašenosti`) | |
| Containment | string(2000)? | N (`Zadržavanje / trenutna korekcija`) | |
| ProductDecision | enum `ProductDecision` | N (`Odluka o proizvodu`) | `CekaOdluku`, `Dorada`, `Skart`, `PovratDobavljacu`, `OdstupanjeOdobreno` |
| RootCause | string(2000)? | N (`Analiza uzroka`) | |
| CorrectiveAction | string(2000)? | N (`Korektivna radnja`) | |
| ActionOwnerEmployeeId | FK → Employee? | N (`Vlasnik radnje`) | |
| DueDate | DateOnly? | N (`Rok`) | |
| EffectivenessCheckDate | DateOnly? | N (`Datum provjere efektivnosti`) | |
| Status | enum `NcStatus` | N (`Status`) | `Otvorena`, `RadnjaUToku`, `ProvjeraEfektivnosti`, `Zatvorena` |
| EvidenceFiles | M:N → FileAttachment | N (`Dokaz / zapis`) | spojna tabela `non_conformity_evidence` |

Uklonjeno: `Broj naloga` (text).

## 12. ControlledDocument — Kontrolisani dokument 🔒 (`Kontrolisani dokumenti`)

| Polje | Tip | Izvor | Napomena |
|---|---|---|---|
| Title | string(200), req | N (`Naziv dokumenta`) | |
| Code | string(40), req | N (`Oznaka dokumenta`) | npr. `PR-01`; više verzija dijeli isti Code |
| Kind | enum `DocumentKind` | N (`Vrsta`) | `Procedura`, `RadnoUputstvo`, `ObrazacZapis`, `EksterniDokument` |
| Version | string(10), req | N (`Verzija`) | unique (`Code`, `Version`); u bazi i jedinstveni indeks: najviše jedna neponištena `Odobren` po `Code` |
| Status | enum `DocumentStatus` | N (`Status`) | `Nacrt`, `NaPregledu`, `Odobren`, `Povucen` |
| OwnerEmployeeId | FK → Employee? | N (`Vlasnik dokumenta`) | |
| ApprovalDate | DateOnly? | N | obavezno kad `Odobren` |
| EffectiveDate | DateOnly? | N (`Datum primjene`) | |
| NextReviewDate | DateOnly? | N (`Sljedeća provjera`) | |
| ChangeDescription | string(1000)? | N (`Opis izmjene`) | |
| FileId | FK → FileAttachment? | N (`Važeći dokument`) | |

U kodu se polje `Version` zove `DocVersion` (kolona `version`), jer je `Version` na svakom entitetu
concurrency token (`xmin`). U API-ju: `docVersion`. Jedinstvenost (`Code`, `Version`) važi za neponištene zapise,
da se pogrešno unesena pa poništena verzija može unijeti ponovo.

## 13. Note — Bilješka (NOVO, Notion "Notes" blok na vrhu stranice)

Kratke stavke tipa "Messer plin naručiti", "Hladnjak ispuhati".

| Polje | Tip |
|---|---|
| Text | string(300), req |
| IsDone | bool |
| DoneAt | DateTimeOffset? |

## 14. FileAttachment

| Polje | Tip | Napomena |
|---|---|---|
| OriginalName | string(255) | |
| ContentType | string(100) | |
| SizeBytes | long | max 25 MB |
| StoragePath | string(400) | relativno u storage rootu, ime = Guid; nikad originalno ime na disku |
| Sha256 | string(64) | |

Dozvoljeno: `.pdf .dxf .dwg .step .stp .png .jpg .jpeg .xlsx .docx`.

Provjera na serveru pri uploadu (sve mora proći, inače 400 sa razlogom):
1. ekstenzija je na listi iznad (bez obzira na velika/mala slova);
2. veličina 1 B … 25 MB (prekida se čitanje čim pređe);
3. sadržaj odgovara ekstenziji:

| Ekstenzija | Provjera sadržaja |
|---|---|
| `.pdf` | počinje sa `%PDF-` |
| `.png` | potpis `89 50 4E 47 0D 0A 1A 0A` |
| `.jpg` `.jpeg` | počinje sa `FF D8 FF` |
| `.xlsx` `.docx` | ZIP potpis `50 4B 03 04` i u arhivi postoji `xl/workbook.xml` odnosno `word/document.xml` |
| `.dwg` | počinje sa `AC10` (AutoCAD verzijski header, npr. `AC1032`) |
| `.step` `.stp` | počinje sa `ISO-10303-21;` (dozvoljen BOM i bijeli prostor ispred) |
| `.dxf` | ASCII DXF: prvih 4 KB je tekst (bez NUL bajtova) i sadrži `SECTION`; binarni DXF: počinje sa `AutoCAD Binary DXF` |

`ContentType` se postavlja na serveru prema ekstenziji, ne prema onome što pošalje browser.
Ime fajla na disku je Guid, originalno ime se čuva samo u `OriginalName` (za download).

## 15. AuditLog

| Polje | Tip |
|---|---|
| EntityType | string(60) |
| EntityId | Guid |
| Action | enum `Create`, `Update`, `Void` |
| ChangesJson | jsonb — `{polje: [staro, novo]}`; enum vrijednosti se upisuju imenom (`"Odobren"`), razlog kao `"reason": [null, tekst]` |
| UserId | Guid? — `null` = sistemska akcija |
| At | DateTimeOffset |

Puni se automatski iz EF `SaveChangesInterceptor`, nikad ručno iz endpointa.
Izuzetak od baznog entiteta: ima `Id` (Guid v7), ali **nema** `CreatedAt/By`, `UpdatedAt/By`
i sam se ne audituje.

## 16. NumberSequence (`number_sequence`)

Tehnička tabela brojača za numeraciju (DOMAIN §6). Nema audit polja, ne audituje se.

| Polje | Tip | Napomena |
|---|---|---|
| Kind | string(10), PK dio | prefiks: `RN`, `DP`, `KK`, `NC` |
| Period | string(8), PK dio | `YYYY` ili `YYYYMMDD` (za `DP`) |
| LastValue | int | zadnji izdani broj |

PK (`Kind`, `Period`).

---

## Enumi — kompletna lista

```
WorkOrderStatus:     Zaprimljen, CekaMaterijal, Spreman, Laser, Presanje, Kontrola,
                     Doraditi, Zavrseno, Isporuceno, Otkazano
Priority:            Normalan, Hitno
QualityApproval:     Ceka, Prihvaceno, Blokirano
SupplierApprovalStatus: UProcjeni, Odobren, UslovnoOdobren, Blokiran
ReceiptDecision:     NaKontroli, Prihvacen, Karantin, Odbijen
Unit:                kg, kom, m, tabla, drugo
StockMovementType:   Prijem, Izdavanje, Korekcija, Otpis
EquipmentKind:       Masina, MjernoSredstvo
InspectionPhase:     UlazniMaterijal, LaserskoRezanje, HidraulicnoPresanje, ZavrsnaKontrola
InspectionResult:    Ceka, Prihvaceno, Neuskladjeno
NcSource:            Dobavljac, UlazniMaterijal, Laser, Presanje, ZavrsnaKontrola, Kupac, Audit, Ostalo
ProductDecision:     CekaOdluku, Dorada, Skart, PovratDobavljacu, OdstupanjeOdobreno
NcStatus:            Otvorena, RadnjaUToku, ProvjeraEfektivnosti, Zatvorena
DocumentKind:        Procedura, RadnoUputstvo, ObrazacZapis, EksterniDokument
DocumentStatus:      Nacrt, NaPregledu, Odobren, Povucen
CalibrationResult:   Uskladjeno, Neuskladjeno
CalibrationStatusAtCheck: UKalibraciji, VanKalibracije, BezDatumaKalibracije, BezMjernogSredstva
```

Prikazni nazivi (sa dijakriticima: "Čeka materijal", "Isporučeno", "Neusaglašeno"…) žive
samo na frontendu u `web/src/lib/labels.ts`, jedan izvor. Vrijednosti 1:1 odgovaraju Notion
opcijama iz `NOTION_SOURCE.md`.

## Indeksi (minimum)

- `work_order (status, due_date)` — dashboard i liste
- `production_entry (date)`, `production_entry (work_order_id)`
- `stock_movement (material_lot_id)`
- `material_lot (material_item_id, receipt_decision)`
- `quality_check (work_order_id, phase, result)`
- `non_conformity (status)`
- `equipment_calibration (equipment_id, calibration_date)`
