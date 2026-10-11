namespace Tds.Api.Domain;

// DATA_MODEL "Enumi". Stored as strings; display names live only in web/src/lib/labels.ts.

public enum WorkOrderStatus { Zaprimljen, CekaMaterijal, Spreman, Laser, Presanje, Kontrola, Doraditi, Zavrseno, Isporuceno, Otkazano }
public enum Priority { Normalan, Hitno }
public enum QualityApproval { Ceka, Prihvaceno, Blokirano }
public enum SupplierApprovalStatus { UProcjeni, Odobren, UslovnoOdobren, Blokiran }
public enum ReceiptDecision { NaKontroli, Prihvacen, Karantin, Odbijen }
#pragma warning disable CA1707, IDE1006 // values match DATA_MODEL exactly (lowercase units)
public enum Unit { kg, kom, m, tabla, drugo }
#pragma warning restore CA1707, IDE1006
public enum StockMovementType { Prijem, Izdavanje, Korekcija, Otpis }
public enum EquipmentKind { Masina, MjernoSredstvo }
public enum InspectionPhase { UlazniMaterijal, LaserskoRezanje, HidraulicnoPresanje, ZavrsnaKontrola }
public enum InspectionResult { Ceka, Prihvaceno, Neuskladjeno }
public enum NcSource { Dobavljac, UlazniMaterijal, Laser, Presanje, ZavrsnaKontrola, Kupac, Audit, Ostalo }
public enum ProductDecision { CekaOdluku, Dorada, Skart, PovratDobavljacu, OdstupanjeOdobreno }
public enum NcStatus { Otvorena, RadnjaUToku, ProvjeraEfektivnosti, Zatvorena }
public enum DocumentKind { Procedura, RadnoUputstvo, ObrazacZapis, EksterniDokument }
public enum DocumentStatus { Nacrt, NaPregledu, Odobren, Povucen }
public enum CalibrationResult { Uskladjeno, Neuskladjeno }
public enum CalibrationStatusAtCheck { UKalibraciji, VanKalibracije, BezDatumaKalibracije, BezMjernogSredstva }
