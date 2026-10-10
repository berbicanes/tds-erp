# UI — ekrani i ponašanje

Klijentov zahtjev doslovno: *"Da imam sve informacije naprijed — dashboard, stanje materijala,
zalihe, dnevna proizvodnja, nalozi. Osnovno nešto."* Notion mu je bio "najlakši za kontrolu"
→ tabele, statusi kao obojeni badgevi, brz unos. Ne praviti "enterprise" UI.

## 1. Principi

- **Gustina umjesto dekoracije.** Tabele sa puno redova, mali padding, bez kartica oko svega.
- **Status = boja + tekst**, nikad samo boja.
- **Svaki broj je klikabilan** do liste koja ga čini (Kasni: 3 → filtrirana lista naloga).
- **Prazno stanje** jasno: "Nema naloga koji kasne." — ne prazan prostor, ne `0` kad je `—`.
- Svjetla tema je primarna (kancelarija + hala na dnevnom svjetlu). Tamna nije u obimu.
- Font: sistemski stack (bez Google Fonts — zabrana vanjskih servisa). Brojevi `tabular-nums`.

## 2. Navigacija

Lijevi sidebar (desktop), donja traka sa 4 stavke (mobilni):

```
Pregled (dashboard)
Nalozi
Dnevna proizvodnja
Zalihe            ← artikli + lotovi + pokreti
Kvalitet          ← kontrole, neusaglašenosti
Dobavljači
Oprema            ← mašine + mjerna sredstva
Dokumenti
Podešavanja       ← zaposlenici, korisnici (Admin)
```
Mobilni donja traka: Pregled · Brzi unos · Nalozi · Zalihe.

## 3. Pregled (dashboard) — početni ekran

Jedan API poziv `GET /api/dashboard` → jedan DTO. Osvježavanje svakih 60 s (TanStack
`refetchInterval`) i na fokus prozora. Prikaz "Ažurirano HH:mm".

Raspored (desktop, 12-kolonski grid; mobilni: sve jedno ispod drugog istim redom):

**Red 1 — KPI pločice (6):**
| Pločica | Vrijednost | Klik vodi na |
|---|---|---|
| Aktivni nalozi | broj naloga ∉ {Zavrseno, Isporuceno, Otkazano} | Nalozi, filter aktivni |
| Kasne | `IsOverdue` (crveno ako > 0) | Nalozi, filter kasne |
| Rok u 7 dana | aktivni sa `DueDate` ≤ danas+7 | Nalozi, filter |
| Hitni | aktivni sa `Priority = Hitno` | Nalozi, filter |
| Danas gotovo | Σ GoodQty danas | Dnevna proizvodnja, danas |
| Materijal ispod minimuma | broj artikala `Ispod minimuma` ili `Nema` | Zalihe, filter |

**Red 2 — Danas u proizvodnji** (lijevo 8 kolona):
Tabela dnevnih zapisa za danas: operater, nalog, laser, presa, gotovo, škart, sati.
Zbir na dnu. Ispod: stupčasti graf zadnjih 14 radnih dana — laser / presa / gotovo
(Recharts, grupisani stupci), škart kao linija. Prazni dani se prikazuju kao 0 samo ako
je dan prošao; budući/vikend se ne prikazuju.

**Red 2 desno — Upozorenja** (4 kolone), lista sortirana po hitnosti:
- lotovi `NaKontroli` / `Karantin`
- otvorene NC (sa rokom)
- `QualityApproval = Blokirano` nalozi
- rokovi ≤ 30 dana iz `DOMAIN.md` §7.6 (kalibracija, ocjena dobavljača, provjera dokumenta, popis)
- mjerna sredstva van kalibracije
Svaka stavka: ikona, tekst, datum/rok, link.

**Red 3 — Nalozi po roku** (puna širina):
Aktivni nalozi sortirani po `DueDate`. Kolone: broj, kupac, proizvod, status (badge),
prioritet, napredak (traka `CompletedQty / OrderedQty`), rok (crveno kad kasni, narandžasto
≤ 3 dana), kvalitet (ikone: prvi komad laser ✓/–, presa ✓/–, završna ✓/–).
Max 15 redova + "Svi nalozi →".

**Red 4 — Zalihe** (puna širina):
Artikli sa nivoom ≠ OK prvo, zatim ostali. Kolone: artikal, kvalitet, debljina,
na stanju, rezervisano, raspoloživo, minimum, nivo (badge). Max 10 + "Sve zalihe →".

**Bilješke** — mala lista (Notion "Notes"): dodaj u jednom polju + Enter, checkbox za
završeno, završene se sakriju nakon 7 dana.

## 4. Brzi unos (hala, mobilni) — `/brzi-unos`

Najvažniji ekran za usvajanje. Ako je sporiji od papira, neće se koristiti.

- Cilj: zapis za **< 20 sekundi**, palcem, sa rukavicama → dugmad min 48 px.
- Polja redom:
  1. Nalog — velika lista aktivnih naloga (broj + kupac + proizvod), pretraga, zadnja 3 korištena na vrhu
  2. Operater — zadnji izabrani zapamćen na uređaju (localStorage, try/catch)
  3. Oprema — chipovi M-01…M-04, multi-select, zadnje zapamćeno
  4. Lot — samo lotovi vezani za nalog + `Prihvacen`; prikazuje se tek kad je laser > 0
  5. Brojevi: Laser · Presa · Gotovo · Škart — stepper (− / broj / +) + direktan unos, `inputmode="numeric"`
  6. Sati, utrošak materijala (opcionalno, sklopljeno), napomena (sklopljeno)
- Datum = danas, prikazan ali ne fokusiran.
- `Gotovo` je onemogućeno sa objašnjenjem kad nalog nema prihvaćenu završnu kontrolu (DOMAIN §5.3).
- Nakon spremanja: zeleni toast "Spremljeno DP-…", forma ostaje sa istim nalogom/operaterom
  za sljedeći unos.
- Greške servera prikazati iznad dugmeta, ne samo u toastu.
- Testirati na 375×812 i 360×740.

## 5. Liste (Nalozi, Lotovi, Kontrole, NC, …)

Zajednička `DataTable` komponenta:
- sortiranje po koloni, pretraga (debounce 300 ms), filteri kao chipovi iznad tabele
- filter i sortiranje u URL query (dijeljivi linkovi, back dugme radi)
- paginacija na serveru, 50 po strani
- na mobilnom: redovi postaju kartice sa 3–4 najbitnija polja
- export u CSV (UTF-8 sa BOM, separator `;`, decimalni zarez — da Excel u BiH otvori ispravno)

## 6. Detalj naloga

Zaglavlje: broj, kupac, proizvod, status badge + dugme "Promijeni status" (nudi samo
dozvoljene prelaze; nedozvoljeni nisu prikazani; gate koji ne prolazi prikazan sivo sa razlogom).
Kartice ispod (tabovi na mobilnom):
- Osnovno (polja, crtež sa preview/download)
- Proizvodnja (dnevni zapisi tog naloga + zbirovi)
- Materijal (vezani lotovi, rezervacije, utrošeno)
- Kvalitet (kontrole po fazama, NC)
- Istorija (AuditLog, čitljivo: "12.10.2026. 14:32 — Anes: Status Laser → Presanje")

## 7. Forme

- Labela iznad polja, obavezno označeno `*`, greška ispod polja crveno.
- Datum: nativni `<input type="date">`, prikaz `dd.MM.yyyy.`.
- Brojevi: prihvata i zarez i tačku kao decimalni separator, prikaz zarezom.
- Izbor zaposlenika/lota/dobavljača: combobox sa pretragom.
- Upload: drag & drop + dugme, prikaz imena, veličine, uklanjanje prije spremanja.
- Napuštanje forme sa nesnimljenim izmjenama → potvrda.

## 8. Boje statusa (Tailwind tokeni, jedan izvor u `labels.ts`)

| Značenje | Boja | Statusi |
|---|---|---|
| Neutralno / čeka | slate | Zaprimljen, Čeka, Nacrt, Na kontroli, U procjeni |
| U radu | blue | Spreman, Laser, Presanje, Kontrola, Radnja u toku, Na pregledu |
| Pažnja | amber | Čeka materijal, Doraditi, Hitno, Nisko, Uslovno odobren, Provjera efektivnosti |
| Problem | red | Blokirano, Blokiran (dobavljač), Karantin, Odbijen, Neusaglašeno, Ispod minimuma, Nema, Otvorena NC, kasni |
| Gotovo | green | Završeno, Prihvaćeno, Prihvaćen, Odobren, Zatvorena, OK |
| Arhiva | zinc (prigušeno) | Isporučeno, Otkazano, Povučen |

Sve vrijednosti koje nisu navedene u tabeli (npr. `Normalan`, faze kontrole, izvori NC, odluke o
proizvodu, vrste dokumenata, jedinice, vrste opreme, tipovi pokreta) su **slate**. `Hitno` ostaje amber.
Prikazni nazivi enuma su 1:1 iz `NOTION_SOURCE.md`; jedinice ostaju malim slovima
(`kg`, `kom`, `m`, `tabla`, `drugo`).

Kontrast teksta na badge pozadini ≥ 4.5:1.

## 9. Tekstovi

Sve na bosanskom, ijekavica, kratko i konkretno. Greške kažu šta ne valja i šta uraditi:
✗ "Validation failed" · ✓ "Lot L-2291 je u karantinu i ne smije se koristiti."
Bez "Molimo Vas" i bez uzvičnika. Persiranje se ne koristi (alat za radionicu).
