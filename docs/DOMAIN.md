# DOMAIN — poslovna pravila

Sva pravila ovdje se provode **na serveru** (Domain sloj + validatori). Frontend ih može
odražavati radi UX-a, ali nikad nije jedina provjera. Svako pravilo ima unit test.

## 1. Pojmovnik (UI bosanski ↔ kod engleski)

| UI | Kod | Značenje |
|---|---|---|
| Nalog (radni nalog, nalog proizvodnje) | `WorkOrder` | jedna narudžba kupca za jedan proizvod |
| Dnevni zapis proizvodnje | `ProductionEntry` | šta je jedan operater uradio na jednom nalogu jedan dan |
| Artikal materijala | `MaterialItem` | vrsta + kvalitet + debljina (npr. Lim inox 1.4301, 3 mm) |
| Lot / ulazni lot / heat broj | `MaterialLot` | konkretna isporuka materijala sa certifikatom |
| Pokret zalihe | `StockMovement` | prijem, izdavanje, korekcija, otpis |
| Rezervacija | `WorkOrderMaterial.ReservedQty` | količina lota obećana nalogu |
| Dobavljač | `Supplier` | |
| Kontrola kvaliteta / zapis kontrole | `QualityCheck` | |
| Prvi komad | `QualityCheck` faze Laser/Presanje | potvrda prvog komada prije serije |
| Završna kontrola | `QualityCheck` faze `ZavrsnaKontrola` | |
| Neusaglašenost, NC, CAPA | `NonConformity` | |
| Mjerno sredstvo | `Equipment` (Kind = MjernoSredstvo) | pomično mjerilo, mikrometar… |
| Oprema / mašina | `Equipment` (Kind = Masina) | M-01…M-04 |
| Kontrolisani dokument | `ControlledDocument` | procedura, uputstvo, obrazac |
| Škart | `ScrapQty` | komadi za otpad ili doradu |
| Operater | `Employee` | radnik u hali (ne mora imati login) |

## 2. Zalihe

### 2.1 Stanje se računa iz pokreta
`OnHand(lot) = Σ StockMovement.Quantity` za taj lot (neponišteni pokreti).
Nema kolone "stanje" koja se ažurira. Ako performanse ikad zatraže keš — materijalizovani
pogled, ne ručna kolona. (Za ovu radionicu neće trebati: stotine lotova, hiljade pokreta.)

### 2.2 Kada nastaju pokreti
| Događaj | Pokret | Količina |
|---|---|---|
| Kreiran lot | `Prijem` | `+ReceivedQty` |
| Dnevni zapis sa `MaterialConsumed` | `Izdavanje` (veže `ProductionEntryId`, `WorkOrderId`) | `−MaterialConsumed` |
| Izmjena `MaterialConsumed` na zapisu | postojeći pokret se poništava, kreira se novi | — |
| Poništen dnevni zapis | njegov pokret se poništava | — |
| Popis / greška | `Korekcija` (razlog obavezan) | ± razlika |
| Lot prelazi u `Odbijen` | `Otpis` **automatski**, razlog obavezan (unosi korisnik pri odluci) | −cijelo preostalo stanje |
| Otpis ostatka / dijela | `Otpis` (razlog obavezan) | −količina, najviše OnHand |

Sve u **istoj transakciji** sa zapisom koji ih izaziva.

Ručni pokreti (Kontrolor/Admin):
- `Korekcija`: ± količina, razlog obavezan; rezultat ne smije spustiti OnHand ispod 0.
- `Otpis`: količina > 0 i ≤ OnHand, razlog obavezan.
- Datum pokreta: danas ili ranije, nikad u budućnosti.
- `ReceivedQty` na lotu se ne mijenja nakon kreiranja — greška u prijemu se ispravlja `Korekcijom`.

### 2.3 Rezervacije
- `Reserved(lot) = Σ WorkOrderMaterial.ReservedQty` za naloge koji nisu `Zavrseno`,
  `Isporuceno`, `Otkazano`.
- Rezervacija se smanjuje za utrošak: efektivna rezervacija po nalogu =
  `max(0, ReservedQty − Σ izdavanja tog lota za taj nalog)`.
- `Available(lot) = OnHand − efektivne rezervacije`. Smije biti negativno samo kao upozorenje
  (prikaz crveno), ne blokira unos — radionica je stvarnost, sistem je evidencija.
- Rezervisati se može lot sa odlukom `NaKontroli` ili `Prihvacen` (ne `Karantin`, `Odbijen`).
  Rezervacija veća od `Available` je dozvoljena uz upozorenje. Utrošak je i dalje samo iz `Prihvacen`.
- Veza lot–nalog (`WorkOrderMaterial`) se ne uklanja (sljedivost); rezervacija se može smanjiti do 0.

### 2.4 Nivo zalihe (po artiklu)
`OnHand`, `Reserved`, `Available` i Nivo zalihe po artiklu računaju se **samo** nad lotovima sa
`ReceiptDecision = Prihvacen`. Posebno se prikazuje **"Na kontroli / karantin"** = Σ OnHand lotova sa
odlukom `NaKontroli` ili `Karantin` (materijal fizički u hali, ali još nije upotrebljiv) — ne ulazi
u `Available` ni u Nivo zalihe.

Nivo zalihe:
- `Available ≤ 0` → **Nema** — bez obzira na minimum (i kad `MinimumStock` nije postavljen):
  stanje 0 je činjenica i korisnija informacija od "Nije definisano".
- `MinimumStock` postoji i `Available < MinimumStock` → **Ispod minimuma**
- `MinimumStock` postoji i `Available < 1.5 × MinimumStock` → **Nisko**
- inače → **OK**
- `MinimumStock` nije postavljen, a `Available > 0` → **Nije definisano** (ne "OK")

Notion formula `Nivo zalihe` nije bila čitljiva iz snimka — ovo je naša definicija.
Potvrditi pragove sa klijentom (vidi §9).

### 2.5a Odluka prijema — dozvoljeni prelazi
| Iz | U |
|---|---|
| NaKontroli | Prihvacen, Karantin, Odbijen |
| Karantin | Prihvacen, Odbijen |
| Prihvacen | Karantin (naknadno uočena greška) |
| Odbijen | — (konačno) |

- Odluka ≠ `NaKontroli` zahtijeva `InspectedByEmployeeId` (§7.2).
- Prelaz u `Odbijen` automatski otpisuje cijelo preostalo stanje (§2.2), razlog obavezan.
- Poništavanje lota (samo Admin, razlog obavezan): dozvoljeno samo ako lot nema drugih
  neponištenih pokreta osim `Prijem` i nije vezan ni za jedan nalog. Poništava i `Prijem`.

### 2.5 Izdavanje iz lota
Dozvoljeno samo iz lota sa `ReceiptDecision = Prihvacen`. `Karantin`, `NaKontroli`, `Odbijen`
→ odbiti sa porukom "Lot {broj} nije prihvaćen ({odluka}) i ne smije se koristiti."
Izdavanje veće od `OnHand` → odbiti ("Na lotu ima samo X {jedinica}").

## 3. Izvedene vrijednosti na nalogu
- `CompletedQty = Σ GoodQty` neponištenih dnevnih zapisa.
- `LaserFirstPieceApproved` = postoji neponištena `QualityCheck` (Phase = `LaserskoRezanje`,
  Result = `Prihvaceno`) za nalog. Analogno `PressFirstPieceApproved`, `FinalInspectionPassed`.
- `ReadyForShipment = FinalInspectionPassed && QualityApproval == Prihvaceno && CompletedQty ≥ OrderedQty`.
- `Progress % = min(100, CompletedQty / OrderedQty × 100)`, cijeli broj.
- `IsOverdue = DueDate < danas (Europe/Sarajevo) && Status ∉ {Zavrseno, Isporuceno, Otkazano}`.

Ove vrijednosti se vraćaju u DTO-u, nikad se ne primaju od klijenta.

## 4. Status naloga

```
Zaprimljen ─→ CekaMaterijal ─→ Spreman ─→ Laser ─→ Presanje ─→ Kontrola ─→ Zavrseno ─→ Isporuceno
     │              ↑             │          │          │           │
     └──────────────┴─────────────┘          └──────────┴──→ Doraditi ──→ (Laser | Presanje | Kontrola)
Bilo koji status osim Isporuceno ─→ Otkazano
```

Dozvoljeni prelazi (sve ostalo → 409 sa porukom):
| Iz | U |
|---|---|
| Zaprimljen | CekaMaterijal, Spreman, Otkazano |
| CekaMaterijal | Spreman, Otkazano |
| Spreman | CekaMaterijal, Laser, Presanje, Otkazano |
| Laser | Presanje, Kontrola, Doraditi, Otkazano |
| Presanje | Kontrola, Doraditi, Otkazano |
| Kontrola | Zavrseno, Doraditi, Otkazano |
| Doraditi | Laser, Presanje, Kontrola, Otkazano |
| Zavrseno | Isporuceno, Kontrola (povrat) |
| Isporuceno | — (konačno) |
| Otkazano | — (konačno; ponovno otvaranje samo Admin, vraća u Zaprimljen) |

Uslovi (gate) za prelaz:
- → `Presanje` iz `Laser`: `LaserFirstPieceApproved`. Ako nije: odbiti sa
  "Prvi komad sa lasera nije potvrđen."
- → `Kontrola` iz `Presanje`: `PressFirstPieceApproved`.
- → `Zavrseno`: `FinalInspectionPassed && QualityApproval == Prihvaceno`.
- → `Isporuceno`: `ReadyForShipment`.
- `QualityApproval = Blokirano` blokira **sve** prelaze naprijed osim u `Doraditi` i `Otkazano`.
- Nalog koji ne ide kroz laser ili presu (npr. samo plazma/varenje) — vidi otvoreno pitanje §9.1.
  Do odgovora: Admin može preskočiti gate uz obavezan razlog koji ide u AuditLog
  (`override` sa `reason`). Ne graditi ništa više od toga.
  Override važi **samo** za dva gate-a prvog komada (Laser → Presanje, Presanje → Kontrola).
  Gate-ovi za `Zavrseno` i `Isporuceno` i blokada `Blokirano` se ne mogu preskočiti.
- "Unazad" su samo `Spreman → CekaMaterijal` i `Zavrseno → Kontrola` (povrat); svi ostali prelazi
  su "naprijed". Uz `QualityApproval = Blokirano` dozvoljeni su samo ta dva prelaza unazad,
  `→ Doraditi` i `→ Otkazano`.
- Razlog (override, ponovno otvaranje otkazanog naloga) se ne čuva u koloni naloga: endpoint ga
  upisuje u scoped audit context, a `AuditInterceptor` ga dodaje u `ChangesJson` kao ključ `reason`.
- Nalog u `Isporuceno` ili `Otkazano` je read-only, osim polja `Notes`.
- `QualityApproval` se mijenja posebnom akcijom (Kontrolor/Admin), ne kroz formu naloga.

Svaki prelaz se loguje (AuditLog). Ne postoji automatski prelaz statusa — status mijenja
čovjek; sistem samo provjerava i predlaže (npr. badge "Spremno za završetak").

## 5. Dnevni zapis proizvodnje

5.1 Nalog mora biti u statusu `Spreman`, `Laser`, `Presanje`, `Kontrola` ili `Doraditi`.
Za ostale: "Na nalogu {broj} sa statusom {status} se ne može evidentirati proizvodnja."

5.2 Ako `LaserCutQty > 0` ili `MaterialConsumed > 0` → `MaterialLotId` obavezan, lot mora biti
`Prihvacen` (§2.5), i lot mora biti vezan za nalog (`WorkOrderMaterial`) — ako nije, veza se kreira
automatski sa `ReservedQty = 0` (sljedivost je važnija od strogosti forme).
Oprema: barem jedna aktivna mašina (`Kind = Masina`).

5.3 **Pravilo klijenta:** "Gotove ispravne komade upiši tek nakon završne kontrole."
→ `GoodQty > 0` dozvoljeno samo ako nalog ima `FinalInspectionPassed`. Inače odbiti sa:
"Gotovi ispravni komadi se upisuju tek nakon prihvaćene završne kontrole."
(Pravilo je doslovno iz Notiona. Ako u praksi smeta — §9.2.)

5.4 `Date` ne smije biti u budućnosti ni starije od 7 dana (osim Admin).

5.5 Upozorenje (ne greška) ako `Σ GoodQty` nakon zapisa prelazi `OrderedQty`.

5.6 Izmjena zapisa: autor isti dan, Kontrolor/Admin uvijek. Brisanje ne postoji — poništavanje
sa razlogom, koje poništava i vezani `StockMovement`.
- "Autor" = korisnik koji je unio zapis (`CreatedBy`). Operatera u zapisu (`OperatorEmployeeId`)
  bira slobodno — jedan telefon u hali dijele više radnika.
- "Isti dan" = isti kalendarski dan kao kreiranje zapisa (`CreatedAt`, Europe/Sarajevo).
- Nalog se na postojećem zapisu ne mijenja: zapis se poništi i unese novi.
- Poništavanje: autor isti dan, Kontrolor/Admin uvijek (isto kao izmjena).

## 6. Numeracija

| Entitet | Format | Primjer |
|---|---|---|
| WorkOrder | `RN-{YYYY}-{NNNN}` | RN-2026-0042 |
| ProductionEntry | `DP-{YYYYMMDD}-{NN}` | DP-20261012-03 |
| QualityCheck | `KK-{YYYY}-{NNNN}` | KK-2026-0118 |
| NonConformity | `NC-{YYYY}-{NNN}` | NC-2026-007 |

Brojač po (vrsta, godina/dan) u tabeli `number_sequence`, inkrement `SELECT … FOR UPDATE`
u istoj transakciji. Godina = godina datuma dokumenta po Europe/Sarajevo. Bez rupa nije
zahtjev; bez duplikata jeste.
Kad brojač pređe širinu formata, broj se samo proširi (nema prelijevanja ni greške):
`DP-20261012-100`, `NC-2026-1000`, `RN-2026-10000`. Format potvrditi (§9.4) — možda Sanel već ima svoju šemu.

## 7. Kvalitet (QMS)

7.0 Kontrola se ne uređuje: pogrešna kontrola se poništi (samo Admin, razlog obavezan) i unese
nova. `InspectionDate` ne smije biti u budućnosti (isto kao dnevni zapis i pokret zalihe). Kreira Kontrolor/Admin. (Faza 1: samo kontrole na nalogu; `UlazniMaterijal` dolazi u Fazi 3.)

7.1 `QualityCheck` sa `Result = Neuskladjeno` → u istoj akciji UI nudi kreiranje NC
(predpopunjeno: izvor po fazi, nalog, lot, kontrola). Ne kreira se automatski bez potvrde.

7.2 Lot: odluka `Prihvacen` zahtijeva `InspectedByEmployeeId`. Upozorenje ako nema
certifikata. Dobavljač `Blokiran` → novi lot se ne može kreirati ("Dobavljač je blokiran").
Dobavljač koji se više ne koristi prebacuje se u `Blokiran`, a razlog se upiše u `Notes`
(dobavljači nemaju `IsActive` i ne brišu se).
`UProcjeni` / `UslovnoOdobren` → dozvoljeno uz žuto upozorenje.

7.1a NC iz kontrole: najviše jedna neponištena NC po kontroli. Ako postoji, UI vodi na nju umjesto nove
ponude. Predpopunjavanje: izvor po fazi (`UlazniMaterijal` → `UlazniMaterijal`, `LaserskoRezanje` →
`Laser`, `HidraulicnoPresanje` → `Presanje`, `ZavrsnaKontrola` → `ZavrsnaKontrola`), nalog, lot,
dobavljač lota, kontrola.

7.3 NC prelazi: `Otvorena → RadnjaUToku → ProvjeraEfektivnosti → Zatvorena`.
- → `RadnjaUToku`: `ProductDecision ≠ CekaOdluku`.
- → `ProvjeraEfektivnosti`: `RootCause` i `CorrectiveAction` popunjeni.
- → `Zatvorena`: `EffectivenessCheckDate` postavljen i nije u budućnosti.
- Povrat korak unazad dozvoljen (Kontrolor/Admin), i iz `Zatvorena` (ponovno otvaranje).
- Koraci se ne preskaču (samo susjedni status naprijed ili nazad).
- NC se uređuje u svim statusima osim `Zatvorena`. `MaterialLotId` i `SupplierId` su opcionalni i za izvor
  `Dobavljac` / `UlazniMaterijal` (samo se nude). Kreira i mijenja Kontrolor/Admin; poništava Admin.

7.4 Kontrolisani dokumenti: za jedan `Code` najviše **jedna** verzija `Odobren`.
Odobravanje nove verzije u istoj transakciji prebacuje prethodnu `Odobren` u `Povucen`.
`Odobren` zahtijeva `ApprovalDate` (ne u budućnosti) i fajl.

| Iz | U |
|---|---|
| Nacrt | NaPregledu |
| NaPregledu | Nacrt, Odobren |
| Odobren | Povucen (ručno; i automatski kad se odobri nova verzija istog `Code`) |
| Povucen | — (konačno) |

`Odobren` i `Povucen` su read-only, osim `NextReviewDate` na `Odobren`. Nova verzija = novi zapis sa istim
`Code` (podaci se kopiraju, status `Nacrt`). Kreira i mijenja Kontrolor/Admin; poništava Admin.

7.5 Kalibracija: istorija je u `EquipmentCalibration` (DATA_MODEL §9a). `LastCalibrationDate`,
`NextCalibrationDate` i stanje kalibracije mjernog sredstva se izvode iz zadnje neponištene kalibracije:
- nema neponištene kalibracije ili zadnja nema `NextCalibrationDate` → `BezDatumaKalibracije`
- zadnja kalibracija sa rezultatom `Neuskladjeno` → `VanKalibracije` (bez obzira na datum)
- `NextCalibrationDate < danas` → `VanKalibracije`
- inače → `UKalibraciji`

Mjerno sredstvo sa `VanKalibracije` ili `BezDatumaKalibracije` → može se izabrati u kontroli, ali kontrola
dobija trajnu oznaku `CalibrationStatusAtCheck` (stanje u trenutku kontrole; kontrola bez mjernog
sredstva → `BezMjernogSredstva`). Server je postavlja pri kreiranju i nikad je ne mijenja. UI prikazuje
upozorenje za `VanKalibracije` ("Mjerno sredstvo van kalibracije") i `BezDatumaKalibracije`.
Ne blokira (ISO traži da se to vidi i obradi, ne da se rad zaustavi softverom).
Kalibraciju bilježi Kontrolor/Admin; poništava Admin.

7.6 Rokovi koji idu na dashboard (≤ 30 dana ili prošli): `Supplier.NextEvaluationDate`,
`Equipment.NextCalibrationDate`, `ControlledDocument.NextReviewDate` (samo `Odobren`),
`NonConformity.DueDate` (nije `Zatvorena`), `MaterialItem.NextStocktakeDate`.
Dobavljači sa statusom `Blokiran` ne dobijaju upozorenje za rok ocjene (blokiran = više se ne koristi, §7.2).
NC bez roka ne ide u upozorenja (forma NC uz prazan rok kaže "NC bez roka se ne prati na dashboardu").
Mjerna sredstva u stanju `VanKalibracije` (i zbog rezultata `Neuskladjeno`) ili `BezDatumaKalibracije` su
crvena upozorenja; kalibracija u narednih 30 dana je narandžasto.

7.7 Fajlovi na QMS zapisima (certifikati dobavljača, dokazi kontrole i NC, certifikat kalibracije, fajl
dokumenta): dodaje Kontrolor/Admin; vezu fajla sa zapisom uklanja samo Admin (bilježi se u AuditLog),
sam fajl ostaje sačuvan.
Neaktivna oprema i neaktivni artikli također ne dobijaju upozorenja za kalibraciju i popis.

## 8. Uloge i ovlasti

| Akcija | Operater | Kontrolor | Admin |
|---|---|---|---|
| Vidi dashboard, naloge, zalihe | ✓ | ✓ | ✓ |
| Bilješke: dodavanje, štikliranje i odštikliravanje (bez izmjene teksta i brisanja) | ✓ | ✓ | ✓ |
| Dnevni zapis: unos, izmjena i poništavanje svog zapisa isti dan | ✓ | ✓ | ✓ |
| Dnevni zapis: izmjena i poništavanje bilo kojeg zapisa, bilo kad | | ✓ | ✓ |
| Kreira/uređuje nalog, mijenja status | | ✓ | ✓ |
| `QualityApproval`, kreiranje kontrola, NC | | ✓ | ✓ |
| Artikli, lotovi, prijem, korekcije i otpis zalihe, rezervacije | | ✓ | ✓ |
| Dobavljači, oprema, dokumenti | | ✓ | ✓ |
| Override gate-a, ponovno otvaranje otkazanog naloga | | | ✓ |
| Korisnici (kreiranje, rola, reset lozinke, deaktivacija), zaposlenici | | | ✓ |
| Poništavanje QMS zapisa **osim dnevnog zapisa** (lot, kontrola, NC, dokument) | | | ✓ |

Dnevni zapis je izuzetak od "poništavanje QMS zapisa samo Admin" — njega poništava autor isti dan
ili Kontrolor/Admin (§5.6).
Korisnički nalog se veže za zaposlenika preko `Employee.UserId`. Deaktiviran korisnik se ne može
prijaviti; korisnici se ne brišu.

Login: korisničko ime + lozinka (min. 10 znakova, barem jedna cifra i jedno malo slovo; bez obaveznih
specijalnih znakova i velikih slova). 5 neuspjelih pokušaja → nalog zaključan 15 minuta.

Pretpostavka: Sanel = Admin. Mala firma — vjerovatno 1–2 Kontrolora, ostalo Operateri.

## 9. Otvorena pitanja za klijenta (ne izmišljati odgovore)

1. Nalozi koji idu samo kroz **plazmu, varenje ili savijanje** — Notion statusi imaju samo
   Laser i Presanje. Trebaju li statusi `Plazma` / `Varenje`, ili se vode kao Laser?
2. Pravilo 5.3 (gotovi komadi tek nakon završne kontrole) — da li se dnevno upisuju samo
   laser/presa, a gotovi jednom na kraju? Ili treba djelimična završna kontrola po seriji?
3. **Utrošak materijala** — kako ga mjeri: table, kg, ostatak table? Bez toga zaliha se ne
   skida sama (`MaterialConsumed` je novo polje).
4. Ima li postojeći format broja naloga?
5. Smjene — opis u Notionu spominje "smjenu", ali polja nema. Treba li?
6. Sekcija "03 · Resursi i QMS" (oprema, kalibracija, kompetencije, ciljevi, rizici, auditi) —
   nedostupna. Šta od toga stvarno koristi?
7. Pragovi "Nisko" / "Ispod minimuma" (§2.4).
8. Ko su korisnici, koliko ih je, ko je Kontrolor?
