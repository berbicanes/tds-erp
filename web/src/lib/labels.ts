/**
 * Enum values (DATA_MODEL "Enumi") → Bosnian display text and badge tone (UI §8).
 * The only place where display names live. Text is 1:1 from NOTION_SOURCE.md.
 * Anything not listed in UI §8 is slate; Priority.Hitno is amber.
 */

export type Tone = "slate" | "blue" | "amber" | "red" | "green" | "zinc";

export interface Label {
  text: string;
  tone: Tone;
}

/** Badge classes per tone. Text/background contrast ≥ 4.5:1 (checked in labels.test.ts). */
export const toneClasses: Record<Tone, string> = {
  slate: "bg-slate-100 text-slate-800",
  blue: "bg-blue-100 text-blue-800",
  amber: "bg-amber-100 text-amber-900",
  red: "bg-red-100 text-red-800",
  green: "bg-green-100 text-green-800",
  zinc: "bg-zinc-100 text-zinc-600",
};

const l = (text: string, tone: Tone = "slate"): Label => ({ text, tone });

export const workOrderStatus = {
  Zaprimljen: l("Zaprimljen"),
  CekaMaterijal: l("Čeka materijal", "amber"),
  Spreman: l("Spreman", "blue"),
  Laser: l("Laser", "blue"),
  Presanje: l("Presanje", "blue"),
  Kontrola: l("Kontrola", "blue"),
  Doraditi: l("Doraditi", "amber"),
  Zavrseno: l("Završeno", "green"),
  Isporuceno: l("Isporučeno", "zinc"),
  Otkazano: l("Otkazano", "zinc"),
} as const satisfies Record<string, Label>;

export const priority = {
  Normalan: l("Normalan"),
  Hitno: l("Hitno", "amber"),
} as const satisfies Record<string, Label>;

export const qualityApproval = {
  Ceka: l("Čeka"),
  Prihvaceno: l("Prihvaćeno", "green"),
  Blokirano: l("Blokirano", "red"),
} as const satisfies Record<string, Label>;

export const supplierApprovalStatus = {
  UProcjeni: l("U procjeni"),
  Odobren: l("Odobren", "green"),
  UslovnoOdobren: l("Uslovno odobren", "amber"),
  Blokiran: l("Blokiran"),
} as const satisfies Record<string, Label>;

export const receiptDecision = {
  NaKontroli: l("Na kontroli"),
  Prihvacen: l("Prihvaćen", "green"),
  Karantin: l("Karantin", "red"),
  Odbijen: l("Odbijen", "red"),
} as const satisfies Record<string, Label>;

export const unit = {
  kg: l("kg"),
  kom: l("kom"),
  m: l("m"),
  tabla: l("tabla"),
  drugo: l("drugo"),
} as const satisfies Record<string, Label>;

export const stockMovementType = {
  Prijem: l("Prijem"),
  Izdavanje: l("Izdavanje"),
  Korekcija: l("Korekcija"),
  Otpis: l("Otpis"),
} as const satisfies Record<string, Label>;

export const equipmentKind = {
  Masina: l("Mašina"),
  MjernoSredstvo: l("Mjerno sredstvo"),
} as const satisfies Record<string, Label>;

export const inspectionPhase = {
  UlazniMaterijal: l("Ulazni materijal"),
  LaserskoRezanje: l("Lasersko rezanje"),
  HidraulicnoPresanje: l("Hidraulično presanje"),
  ZavrsnaKontrola: l("Završna kontrola"),
} as const satisfies Record<string, Label>;

export const inspectionResult = {
  Ceka: l("Čeka"),
  Prihvaceno: l("Prihvaćeno", "green"),
  Neuskladjeno: l("Neusaglašeno", "red"),
} as const satisfies Record<string, Label>;

export const ncSource = {
  Dobavljac: l("Dobavljač"),
  UlazniMaterijal: l("Ulazni materijal"),
  Laser: l("Laser"),
  Presanje: l("Presanje"),
  ZavrsnaKontrola: l("Završna kontrola"),
  Kupac: l("Kupac"),
  Audit: l("Audit"),
  Ostalo: l("Ostalo"),
} as const satisfies Record<string, Label>;

export const productDecision = {
  CekaOdluku: l("Čeka odluku"),
  Dorada: l("Dorada"),
  Skart: l("Škart"),
  PovratDobavljacu: l("Povrat dobavljaču"),
  OdstupanjeOdobreno: l("Odstupanje odobreno"),
} as const satisfies Record<string, Label>;

export const ncStatus = {
  Otvorena: l("Otvorena", "red"),
  RadnjaUToku: l("Radnja u toku", "blue"),
  ProvjeraEfektivnosti: l("Provjera efektivnosti", "amber"),
  Zatvorena: l("Zatvorena", "green"),
} as const satisfies Record<string, Label>;

export const documentKind = {
  Procedura: l("Procedura"),
  RadnoUputstvo: l("Radno uputstvo"),
  ObrazacZapis: l("Obrazac / zapis"),
  EksterniDokument: l("Eksterni dokument"),
} as const satisfies Record<string, Label>;

export const documentStatus = {
  Nacrt: l("Nacrt"),
  NaPregledu: l("Na pregledu", "blue"),
  Odobren: l("Odobren", "green"),
  Povucen: l("Povučen", "zinc"),
} as const satisfies Record<string, Label>;

export type WorkOrderStatus = keyof typeof workOrderStatus;
export type Priority = keyof typeof priority;
export type QualityApproval = keyof typeof qualityApproval;
export type SupplierApprovalStatus = keyof typeof supplierApprovalStatus;
export type ReceiptDecision = keyof typeof receiptDecision;
export type Unit = keyof typeof unit;
export type StockMovementType = keyof typeof stockMovementType;
export type EquipmentKind = keyof typeof equipmentKind;
export type InspectionPhase = keyof typeof inspectionPhase;
export type InspectionResult = keyof typeof inspectionResult;
export type NcSource = keyof typeof ncSource;
export type ProductDecision = keyof typeof productDecision;
export type NcStatus = keyof typeof ncStatus;
export type DocumentKind = keyof typeof documentKind;
export type DocumentStatus = keyof typeof documentStatus;

/** Every enum, for iteration (filters, tests). */
export const enumLabels = {
  workOrderStatus,
  priority,
  qualityApproval,
  supplierApprovalStatus,
  receiptDecision,
  unit,
  stockMovementType,
  equipmentKind,
  inspectionPhase,
  inspectionResult,
  ncSource,
  productDecision,
  ncStatus,
  documentKind,
  documentStatus,
} as const;

/** Label for a value; unknown values are shown as-is in slate, never hidden. */
export function labelOf<T extends Record<string, Label>>(map: T, value: string | null | undefined): Label {
  if (value == null) return { text: "—", tone: "slate" };
  return (map as Record<string, Label>)[value] ?? { text: value, tone: "slate" };
}

/** CreatedBy/UpdatedBy/VoidedBy null = system action (DATA_MODEL). */
export function userLabel(userName: string | null | undefined): string {
  return userName ?? "Sistem";
}
