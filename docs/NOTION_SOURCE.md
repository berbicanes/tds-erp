# NOTION_SOURCE — snimak klijentove šeme

Stranica: **"Proizvodnja i QMS"** (Notion, vlasnik Sanel Dedić)
Snimljeno: 10.10.2026. Status: **test verzija** — klijent priprema finalnu.
Svrha: referenca za import i za poređenje kad stigne finalna verzija. Model aplikacije je
u `DATA_MODEL.md`, ne ovdje.

Kad stigne finalna verzija: napraviti novi snimak ispod ovog, diff po bazama, pa ažurirati
`DATA_MODEL.md` i listu otvorenih pitanja u `DOMAIN.md` §9.

## Struktura stranice

- **Notes** (nabrajanje): "MESSER PLIN NARUCITI DISIK 2X", "NH MAT REZANJE INOX",
  "NH MAT REZANJE ALUMINIJ", "HLADNJAK ISPUHATI"
- **01 · Planiranje:** Nalozi proizvodnje, Dnevna proizvodnja komada
- **02 · Materijal i kvalitet:** Dobavljači, Ulazni materijal u proizvodnji, Zalihe materijala,
  Kontrole kvaliteta, Neusaglašenosti i korektivne radnje
- **03 · Resursi i QMS:** dvije baze **bez pristupa** (vjerovatno oprema/mjerna sredstva i još
  jedna QMS evidencija), Kontrolisani dokumenti

Tekstualna pravila na stranici:
- "Prati svaki posao od važećeg crteža do završne kontrole. Ažuriraj status naloga i evidentiraj
  količine po operaciji, datumu, smjeni i operateru."
- "U dnevnom zapisu poveži Radni nalog, Korišteni materijalni lot i Opremu korištenu. Laser i
  presu vodi kao odvojene operacije."
- "Gotove ispravne komade upiši tek nakon završne kontrole."
- "Za svaki lot zabilježi heat broj, kvalitet, debljinu, certifikat i rezultat ulazne kontrole;
  koristi samo važeće dokumente."
- "Polje Raspoloživo računa stanje nakon rezervacija."
- Sekcija kvaliteta spominje: kompetencije, ciljeve, rizike, povratne informacije, audite,
  održavanje i kalibraciju.

## Baze i svojstva (tip; opcije)

### Nalozi proizvodnje
Broj naloga (title) · Kupac (text) · Proizvod / opis (text) · Kolicina Narudzbe (number) ·
Naručena količina (number) · Odradjeno (number) · Rok isporuke (date) · Datum puštanja (date) ·
Status (status: Zaprimljen, Čeka materijal, Spreman, Laser, Presanje, Kontrola, Doraditi,
Završeno, Isporučeno, Otkazano) · Prioritet (select: Normalan, Hitno) · Odobrenje kvaliteta
(select: Čeka, Prihvaćeno, Blokirano) · Broj crteža (text) · Revizija crteža (text) ·
Važeći crtež (file) · Lot materijala (text) · Materijalni lotovi (relation → Ulazni materijal) ·
Neusaglašenosti / CAPA (relation → Neusaglašenosti) · Odgovorna osoba (person) ·
Laser — prvi komad potvrđen (checkbox) · Presanje — prvi komad potvrđen (checkbox) ·
Završna kontrola prošla (checkbox) · Spremno za isporuku (checkbox)

### Dnevna proizvodnja komada
Zapis proizvodnje (title) · Datum (date) · Broj naloga (text) · Narudzba (text) ·
Radni nalog (relation → Nalozi) · Lot materijala (text) · Korišteni materijalni lot
(relation → Ulazni materijal) · Operater (person) · Oprema korištena (relation → baza bez
pristupa) · Laser — izrezani komadi (number) · Presanje — obrađeni komadi (number) ·
Gotovi ispravni komadi (number) · Škart / za doradu (number) · Radni sati (number) ·
Napomena (text)

### Dobavljači
Naziv dobavljača (title) · Materijal / usluga (text) · Status odobrenja (select: U procjeni,
Odobren, Uslovno odobren, Blokiran) · Datum početne ocjene (date) · Sljedeća ocjena (date) ·
Odgovorna osoba (person) · Certifikati / dokazi (file) · Isporuke i lotovi (relation →
Ulazni materijal) · Kriteriji / rezultat ocjene (text) · Napomene (text)

### Ulazni materijal u proizvodnji
Oznaka lota / heat broj (title) · Dobavljač (text) · Dobavljač u registru (relation →
Dobavljači) · Vrsta materijala (text) · Kvalitet / oznaka (text) · Dimenzije / količina (text) ·
Debljina mm (number) · Datum prijema (date) · Odluka prijema (select: Na kontroli, Prihvaćen,
Karantin, Odbijen) · Broj naloga (text) · Certifikat materijala (file) · Kontrolisao (person) ·
Skladišna lokacija (text) · Dnevni zapisi proizvodnje (relation) · Radni nalozi (relation)

### Zalihe materijala
Materijal / artikal (title) · Kvalitet / oznaka (text) · Debljina mm (number) · Ulazni lot
(relation → Ulazni materijal) · Skladišna lokacija (text) · Jedinica (select: kg, kom, m,
tabla, drugo) · Količina na stanju (number) · Rezervisano (number) · Minimalna zaliha (number) ·
Datum sljedećeg popisa (date) · Raspoloživo (formula) · Nivo zalihe (formula)

### Kontrole kvaliteta
Zapis kontrole (title) · Broj naloga (text) · Radni nalog (relation) · Faza (select: Ulazni
materijal, Lasersko rezanje, Hidraulično presanje, Završna kontrola) · Karakteristika / zahtjev
crteža (text) · Kriterij prihvatanja (text) · Izmjereni rezultat (text) · Rezultat (select:
Čeka, Prihvaćeno, Neusaglašeno) · Kontrolisao (person) · Datum kontrole (date) · Lot materijala
(text) · Mjerna oprema / ID (text) · Mjerno sredstvo (relation → baza bez pristupa) ·
Veličina uzorka (number) · Dokaz / zapis (file)

### Neusaglašenosti i korektivne radnje
NC broj (title) · Broj naloga (text) · Radni nalog (relation) · Izvor (select: Dobavljač,
Ulazni materijal, Laser, Presanje, Završna kontrola, Kupac, Audit, Ostalo) · Opis
neusaglašenosti (text) · Zadržavanje / trenutna korekcija (text) · Odluka o proizvodu
(select: Čeka odluku, Dorada, Škart, Povrat dobavljaču, Odstupanje odobreno) · Analiza
uzroka (text) · Korektivna radnja (text) · Vlasnik radnje (person) · Rok (date) · Datum
provjere efektivnosti (date) · Status (status: Otvorena, Radnja u toku, Provjera
efektivnosti, Zatvorena) · Dokaz / zapis (file)

### Kontrolisani dokumenti
Naziv dokumenta (title) · Oznaka dokumenta (text) · Vrsta (select: Procedura, Radno
uputstvo, Obrazac / zapis, Eksterni dokument) · Verzija (text) · Status (status: Nacrt, Na
pregledu, Odobren, Povučen) · Vlasnik dokumenta (person) · Datum odobrenja (date) · Datum
primjene (date) · Sljedeća provjera (date) · Opis izmjene (text) · Važeći dokument (file)

## Uočeni problemi u Notion šemi (razlog za razlike u DATA_MODEL)

1. Duplikati tekst + relacija: `Broj naloga`, `Lot materijala`, `Dobavljač`, `Mjerna oprema / ID`
   — u aplikaciji samo relacije.
2. Dva polja za količinu narudžbe (`Kolicina Narudzbe`, `Naručena količina`).
3. `Odradjeno`, `Količina na stanju`, `Rezervisano` i checkboxovi kvaliteta su ručni brojevi /
   kvačice — u aplikaciji izvedeni (zlatno pravilo 2).
4. Dnevni zapis nema utrošak materijala → zaliha se ne može skidati automatski. Dodano
   `MaterialConsumed`.
5. Lot nema brojčanu količinu prijema (samo tekst `Dimenzije / količina`). Dodano `ReceivedQty`.
6. `Zalihe materijala` miješa artikal i stanje lota → razdvojeno na `MaterialItem` i pokrete.

## Import (Faza 5) — mapiranje

Jednokratna admin komanda (ne UI), radi nad CSV exportom Notion baza ("Export → Markdown & CSV"):

```bash
dotnet Tds.Api.dll import-notion <folder> --dry-run   # samo izvještaj, ništa se ne upisuje
dotnet Tds.Api.dll import-notion <folder> --apply     # upis, jedna transakcija
```

- Bez `--dry-run` ili `--apply` komanda ništa ne radi (samo uputa). `--apply` prvo uradi istu provjeru i
  **ne upisuje ništa ako postoji ijedna greška**. Dry-run izvrši cijeli upis u transakciji i vrati je nazad, pa
  provjeri i ograničenja baze.
- Fajlovi se traže po početku imena: `Dobavljači`, `Ulazni materijal u proizvodnji`, `Zalihe materijala`,
  `Nalozi proizvodnje` (Notion dodaje id; ako postoji i `_all` varijanta, uzima se ona). Baza bez fajla se preskače.
- Sve zapise kreira "Sistem" (`CreatedBy = null`); svaki ide u AuditLog.
- Ponovno pokretanje istog exporta ne pravi ništa novo: postojeći zapisi su duplikati.

**Vrste nalaza u izvještaju** (red = broj reda u CSV-u, zaglavlje je red 1):

| Nalaz | Kada | Posljedica |
|---|---|---|
| Greška: prazno obavezno polje | npr. `Kupac`, `Rok isporuke`, `Status`, `Jedinica` | blokira `--apply` |
| Greška: nepoznata vrijednost | opcija selecta/statusa koje nema u listi ispod, ili broj/datum u nepodržanom formatu | blokira `--apply` |
| Greška: konflikt | isti ključ dva puta sa različitim podacima (u fajlu ili u odnosu na bazu); dva polja Notiona koja se spajaju u jedno, a razlikuju se | blokira `--apply` |
| Greška: veza nije pronađena | dobavljač, lot, artikal ili (gdje je obavezan) zaposlenik ne postoji ni u bazi ni u exportu | blokira `--apply` |
| Duplikat | isti ključ, isti podaci (u fajlu ili već u bazi) | preskače se |
| Upozorenje | podatak koji se namjerno ne uvozi (fajlovi, ručni `Odradjeno`/`Rezervisano`, kvačice) ili nepoznata osoba u neobaveznom polju | uvozi se bez tog podatka |

**Formati:** datum `2026-10-12`, `October 12, 2026` ili `12.10.2026` (sa ili bez tačke na kraju); datum sa
vremenom ili raspon → nepoznata vrijednost. Broj samo sa decimalnom tačkom (`12.5`), bez separatora hiljada.
Relacija: `Naziv (https://www.notion.so/…)`, više vrijednosti odvojeno zarezom. Osoba (person): puno ime,
mora tačno odgovarati `Employee.FullName` (zaposlenike prvo unijeti u aplikaciji).

| Notion | Ključ | Polja → aplikacija |
|---|---|---|
| Dobavljači | `Naziv dobavljača` | `Materijal / usluga`, `Status odobrenja` (U procjeni, Odobren, Uslovno odobren, Blokiran — obavezno), `Datum početne ocjene`, `Sljedeća ocjena`, `Odgovorna osoba`, `Kriteriji / rezultat ocjene`, `Napomene`. `Certifikati / dokazi` → upozorenje. |
| Zalihe materijala → **MaterialItem** | (`Materijal / artikal`, `Kvalitet / oznaka`, `Debljina mm`) | `Jedinica` (kg, kom, m, tabla, drugo — obavezno), `Minimalna zaliha`, `Datum sljedećeg popisa`. Više redova istog artikla mora imati iste vrijednosti (inače konflikt). |
| Zalihe materijala → **početno stanje** | `Ulazni lot` (tačno jedan lot) | `Količina na stanju` > 0 → pokret `Korekcija` na lotu, datum = dan importa, razlog **"Početno stanje iz Notiona"**. Prazno → upozorenje (stanje nepoznato, nema pokreta); 0 → nema pokreta; < 0, red bez lota ili sa više lotova, odbijen lot sa stanjem → greška. `Rezervisano` > 0 → upozorenje (rezervacije se unose na nalogu). `Raspoloživo`, `Nivo zalihe` se ne uvoze (izvedeno). Lot koji već postoji u bazi ne dobija novu korekciju. |
| Ulazni materijal u proizvodnji → **MaterialLot** | `Oznaka lota / heat broj` | Artikal: iz reda Zaliha koji veže taj lot; ako ga nema, po (`Vrsta materijala`, `Kvalitet / oznaka`, `Debljina mm`). Dobavljač: `Dobavljač u registru`, a kad je prazan, tekst `Dobavljač` (oba popunjena i različita → konflikt). `Datum prijema` (obavezno), `Dimenzije / količina`, `Skladišna lokacija`, `Odluka prijema` (Na kontroli, Prihvaćen, Karantin, Odbijen — obavezno), `Kontrolisao` (obavezno kad odluka nije Na kontroli). `ReceivedQty` = nepoznato (`null`), nema `Prijem` pokreta. `Certifikat materijala` → upozorenje. |
| Nalozi proizvodnje → **WorkOrder** | `Broj naloga` (zadržava se kakav jeste, max 20 znakova) | `Kupac`, `Proizvod / opis`, `Rok isporuke` (obavezno), `Datum puštanja`, `Kolicina Narudzbe` / `Naručena količina` (jedno popunjeno ili oba ista, cijeli broj > 0), `Status` (10 Notion opcija), `Prioritet` (Normalan, Hitno), `Odobrenje kvaliteta` (Čeka, Prihvaćeno, Blokirano), `Broj crteža`, `Revizija crteža`, `Odgovorna osoba`. `Materijalni lotovi` (a kad je prazno, tekst `Lot materijala`) → veza lot–nalog bez rezervacije (`ReservedQty = 0`). Broj u formatu `RN-YYYY-NNNN` pomjera brojač te godine, da novi nalozi ne dobiju isti broj. `Odradjeno` > 0, kvačice kvaliteta, `Važeći crtež`, `Neusaglašenosti / CAPA` → upozorenje (ne uvoze se; računaju se iz zapisa ili se unose posebno). |

Status naloga se uvozi kakav jeste, bez provjere prelaza (DOMAIN §4) — to je istorija, ne prelaz.
Ostale baze (dnevna proizvodnja, kontrole, neusaglašenosti, dokumenti) nisu u obimu importa (ROADMAP Faza 5).
