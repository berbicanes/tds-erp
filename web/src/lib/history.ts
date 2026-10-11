import { EMPTY, formatDate, formatHours, formatPieces, formatQuantity } from "./format";
import type { HistoryChange, HistoryEntry, JsonValue } from "./historyApi";
import {
  calibrationStatusAtCheck, documentKind, documentStatus, inspectionPhase, inspectionResult, labelOf, ncSource, ncStatus, priority,
  productDecision, qualityApproval, receiptDecision, stockMovementType, workOrderStatus, type Label,
} from "./labels";

type Kind = "text" | "date" | "qty" | "pieces" | "hours" | "ref" | "list" | Record<string, Label>;
interface FieldDef { label: string; kind: Kind }

const f = (label: string, kind: Kind = "text"): FieldDef => ({ label, kind });

/**
 * UI §6: Bosnian names of audited fields. The keys are the server property names;
 * HistoryFieldLabelsTests (backend) fails when an audited field of a history record has no entry here.
 */
export const historyFields: Record<string, FieldDef> = {
  Number: f("Broj"),
  Customer: f("Kupac"),
  ProductDescription: f("Proizvod"),
  OrderedQty: f("Naručeno (kom)", "pieces"),
  DueDate: f("Rok", "date"),
  ReleaseDate: f("Datum puštanja", "date"),
  Priority: f("Prioritet", priority),
  QualityApproval: f("Odobrenje kvaliteta", qualityApproval),
  DrawingNumber: f("Broj crteža"),
  DrawingRevision: f("Revizija crteža"),
  DrawingFileId: f("Važeći crtež", "ref"),
  ResponsibleEmployeeId: f("Odgovorna osoba", "ref"),
  Notes: f("Napomena"),
  WorkOrderId: f("Nalog", "ref"),
  MaterialLotId: f("Lot", "ref"),
  Phase: f("Faza", inspectionPhase),
  Characteristic: f("Karakteristika"),
  AcceptanceCriterion: f("Kriterij prihvatanja"),
  MeasuredResult: f("Izmjereni rezultat"),
  SampleSize: f("Veličina uzorka", "pieces"),
  Result: f("Rezultat", inspectionResult),
  InspectedByEmployeeId: f("Kontrolisao", "ref"),
  InspectionDate: f("Datum kontrole", "date"),
  MeasuringEquipmentId: f("Mjerno sredstvo", "ref"),
  CalibrationStatusAtCheck: f("Kalibracija pri kontroli", calibrationStatusAtCheck),
  EvidenceFileIds: f("Dokazi / zapisi", "list"),
  SupplierId: f("Dobavljač", "ref"),
  QualityCheckId: f("Kontrola", "ref"),
  Source: f("Izvor", ncSource),
  Description: f("Opis"),
  Containment: f("Zadržavanje / trenutna korekcija"),
  ProductDecision: f("Odluka o proizvodu", productDecision),
  RootCause: f("Analiza uzroka"),
  CorrectiveAction: f("Korektivna radnja"),
  ActionOwnerEmployeeId: f("Vlasnik radnje", "ref"),
  EffectivenessCheckDate: f("Datum provjere efektivnosti", "date"),
  Date: f("Datum", "date"),
  OperatorEmployeeId: f("Operater", "ref"),
  LaserCutQty: f("Laser (kom)", "pieces"),
  PressedQty: f("Presano (kom)", "pieces"),
  GoodQty: f("Ispravno (kom)", "pieces"),
  ScrapQty: f("Škart (kom)", "pieces"),
  LaborHours: f("Radni sati", "hours"),
  MaterialConsumed: f("Utrošeno", "qty"),
  Note: f("Napomena"),
  EquipmentIds: f("Oprema", "list"),
  ReservedQty: f("Rezervisano", "qty"),
  LotNumber: f("Broj lota / šarže"),
  MaterialItemId: f("Artikal", "ref"),
  ReceivedDate: f("Datum prijema", "date"),
  DimensionsDescription: f("Dimenzije"),
  ReceivedQty: f("Primljeno", "qty"),
  ReceiptDecision: f("Odluka prijema", receiptDecision),
  StorageLocation: f("Lokacija"),
  CertificateFileId: f("Certifikat", "ref"),
  Type: f("Vrsta pokreta", stockMovementType),
  Quantity: f("Količina", "qty"),
  ProductionEntryId: f("Dnevni zapis", "ref"),
  Reason: f("Razlog"),
  Title: f("Naziv dokumenta"),
  Code: f("Oznaka dokumenta"),
  Kind: f("Vrsta", documentKind),
  DocVersion: f("Verzija"),
  OwnerEmployeeId: f("Vlasnik dokumenta", "ref"),
  ApprovalDate: f("Datum odobrenja", "date"),
  EffectiveDate: f("Datum primjene", "date"),
  NextReviewDate: f("Sljedeća provjera", "date"),
  ChangeDescription: f("Opis izmjene"),
  FileId: f("Važeći dokument", "ref"),
  Status: f("Status"),
  IsVoided: f("Poništeno"),
  VoidedReason: f("Razlog poništavanja"),
};

/** The same field name means different enums on different records. */
const statusByEntity: Record<string, Record<string, Label>> = {
  WorkOrder: workOrderStatus,
  NonConformity: ncStatus,
  ControlledDocument: documentStatus,
};

export function fieldDef(entityType: string, field: string): FieldDef {
  if (field === "Status" && statusByEntity[entityType]) return { label: "Status", kind: statusByEntity[entityType] };
  return historyFields[field] ?? { label: field, kind: "text" };
}

export function formatHistoryValue(kind: Kind, value: JsonValue): string {
  if (value === null || value === "" || (Array.isArray(value) && value.length === 0)) return EMPTY;
  if (Array.isArray(value)) return value.map((v) => formatHistoryValue("text", v)).join(", ");
  if (typeof value === "boolean") return value ? "Da" : "Ne";
  if (typeof kind === "object") return labelOf(kind, String(value)).text;
  switch (kind) {
    case "date": return formatDate(String(value));
    case "qty": return formatQuantity(Number(value));
    case "pieces": return formatPieces(Number(value));
    case "hours": return formatHours(Number(value));
    default: return String(value);
  }
}

export interface HistoryLine { label: string; old: string; new: string }

/** Readable lines of one entry: "Status: Laser → Presanje"; on create only the new value. */
export function historyLines(entry: HistoryEntry): HistoryLine[] {
  return entry.changes.map((c: HistoryChange) => {
    const def = fieldDef(entry.entityType, c.field);
    return { label: def.label, old: formatHistoryValue(def.kind, c.old), new: formatHistoryValue(def.kind, c.new) };
  });
}

export const actionText: Record<HistoryEntry["action"], string> = { Create: "Kreirano", Update: "Izmijenjeno", Void: "Poništeno" };
