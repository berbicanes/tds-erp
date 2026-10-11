# Kratko uputstvo — TDS ERP

Prijava: korisničko ime i lozinka koje dobiješ od administratora. Na telefonu dodaj stranicu na početni
ekran (u browseru: "Dodaj na početni ekran") — otvara se pravo na Brzi unos.

## 1. Brzi unos u hali (telefon)

Dole u meniju **Brzi unos**. Datum je uvijek današnji.

1. **Nalog** — dodirni nalog na kome radiš (zadnja tri su na vrhu, ostale nađi pretragom).
2. **Operater** i **oprema** (M-01…M-04) — telefon ih pamti, provjeri da su tačni.
3. **Brojevi** — dugmad **−** / **+** ili upiši broj: Laser, Presa, Gotovo, Škart.
   - **Gotovo** se može upisati tek kad nalog ima prihvaćenu završnu kontrolu; do tada je sivo i piše zašto.
4. **Lot materijala** — pojavi se kad je laser > 0 ili kad upisuješ utrošak. Nude se samo prihvaćeni lotovi
   vezani za nalog. Ako nema nijednog, lot prvo treba vezati na nalogu (tab **Materijal**).
5. **Radni sati** — obavezno; telefon pamti zadnju vrijednost.
6. **Spremi**. Zeleno "Spremljeno DP-…" znači da je zapis upisan; nalog, operater, oprema i sati ostaju za
   sljedeći unos. Ako nešto fali, poruka iznad dugmeta kaže šta.

Pogrešan zapis se ne briše nego poništi (uz razlog) u **Dnevna proizvodnja**: svoj zapis isti dan može poništiti
svako, kasnije samo kontrolor ili admin. Zatim se upiše novi.

## 2. Prijem lota materijala

**Zalihe → Lotovi → Novi prijem**

1. Upiši **heat broj** sa certifikata, izaberi **artikal** i **dobavljača**, datum prijema i **primljenu
   količinu** (u jedinici artikla). Dimenzije i lokacija su opcionalne.
2. **Primi lot** — lot je sada "Na kontroli" i još se ne smije trošiti.
3. Na detalju lota: dodaj **certifikat materijala** i po potrebi **ulaznu kontrolu**.
4. **Promijeni odluku prijema**: Prihvaćen, Karantin ili Odbijen, i ko je kontrolisao. Tek **Prihvaćen** lot
   se može koristiti u proizvodnji. Odbijen lot se automatski otpisuje i to se ne može vratiti.

Stanje lota se nikad ne kuca ručno: računa se iz prijema, utroška, korekcija i otpisa.
Pogrešna količina se ispravlja **korekcijom** na lotu (uz razlog).

**Lot bez količine (iz Notiona):** lot prenesen iz Notiona bez stanja pokazuje "—" za Na stanju, artikal ima oznaku
"Nepotpuno", a lot je u Upozorenjima na Pregledu. Izmjeri stvarno stanje i na lotu upiši **Korekciju** sa tom
količinom (može i 0) i razlogom, npr. "Popis". Od tada je to običan lot; prije toga se iz njega ne može trošiti.

## 3. Status naloga

Na detalju naloga dugme **Promijeni status** nudi samo dozvoljene korake; nedozvoljeni pišu zašto.

Redoslijed: Zaprimljen → Čeka materijal → Spreman → Laser → Presanje → Kontrola → Završeno → Isporučeno
(i Doraditi kad treba popravka, Otkazano u svakom trenutku osim nakon isporuke).

- Laser → Presanje traži potvrđen **prvi komad sa lasera** (kontrola faze Lasersko rezanje, Prihvaćeno).
- Presanje → Kontrola traži potvrđen prvi komad sa prese.
- **Završeno** traži prihvaćenu završnu kontrolu i **Odobrenje kvaliteta = Prihvaćeno** (mijenja ga kontrolor).
- **Isporučeno** traži i da je urađena cijela naručena količina.
- Kad je odobrenje kvaliteta **Blokirano**, nalog može ići samo u Doraditi ili Otkazano.

"Odrađeno" i kvačice kvaliteta na nalogu se ne upisuju — sistem ih računa iz dnevnih zapisa i kontrola.
