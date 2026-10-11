# Noćni log (autonomni rad)

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
