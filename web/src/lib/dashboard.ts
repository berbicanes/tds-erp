import { formatDate } from "./format";
import { itemTitle } from "./material";

/** Minimal shape of a dashboard warning (features/dashboard/api.ts). */
export interface WarningLike {
  kind: string;
  severity: "red" | "amber";
  entityId: string;
  title: string;
  detail: string | null;
  itemName: string | null;
  itemGrade: string | null;
  itemThicknessMm: number | null;
  date: string | null;
}

function daysBetween(from: string, to: string): number {
  return Math.round((Date.parse(to) - Date.parse(from)) / 86_400_000);
}

function dueText(date: string | null, today: string): string {
  if (!date) return "";
  const d = daysBetween(today, date);
  if (d < 0) return `kasni ${-d} ${-d === 1 ? "dan" : "dana"} (${formatDate(date)})`;
  if (d === 0) return `danas (${formatDate(date)})`;
  return `za ${d} ${d === 1 ? "dan" : "dana"} (${formatDate(date)})`;
}

/** Bosnian text of a warning (UI §3, §9: say what is wrong). */
export function warningText(w: WarningLike, today: string): string {
  const item = w.itemName ? itemTitle({ name: w.itemName, grade: w.itemGrade, thicknessMm: w.itemThicknessMm }) : "";
  switch (w.kind) {
    case "LotKarantin":
      return `Lot ${w.title} (${item}) je u karantinu`;
    case "LotNaKontroli":
      return `Lot ${w.title} (${item}) čeka ulaznu kontrolu`;
    case "OrderBlocked":
      return `Nalog ${w.title} (${w.detail ?? ""}) — kvalitet blokiran`;
    case "SupplierEvaluation":
      return w.severity === "red"
        ? `Ocjena dobavljača ${w.title} je istekla — ${dueText(w.date, today)}`
        : `Ocjena dobavljača ${w.title} ${dueText(w.date, today)}`;
    case "Calibration":
      return w.severity === "red"
        ? `Mjerno sredstvo ${w.title} (${w.detail ?? ""}) je van kalibracije — ${dueText(w.date, today)}`
        : `Kalibracija ${w.title} (${w.detail ?? ""}) ${dueText(w.date, today)}`;
    case "CalibrationMissing":
      return `Mjerno sredstvo ${w.title} (${w.detail ?? ""}) nema datum sljedeće kalibracije`;
    case "CalibrationFailed":
      return `Mjerno sredstvo ${w.title} (${w.detail ?? ""}) je van kalibracije — zadnja kalibracija neusklađena`;
    case "DocumentReview":
      return `Provjera dokumenta ${w.title} v${w.itemName ?? ""} (${w.detail ?? ""}) ${dueText(w.date, today)}`;
    case "NcDue":
      return `${w.title} (${w.detail ?? ""}) — rok korektivne radnje ${dueText(w.date, today)}`;
    case "Stocktake":
      return w.severity === "red" ? `Popis artikla ${item} — ${dueText(w.date, today)}` : `Popis artikla ${item} ${dueText(w.date, today)}`;
    default:
      return w.title;
  }
}

/** Where a warning leads (UI §3: every item has a link). */
export function warningHref(w: WarningLike): string {
  switch (w.kind) {
    case "LotKarantin":
    case "LotNaKontroli":
      return `/zalihe/lotovi/${w.entityId}`;
    case "OrderBlocked":
      return `/nalozi/${w.entityId}`;
    case "SupplierEvaluation":
      return `/dobavljaci/${w.entityId}`;
    case "NcDue":
      return `/kvalitet/nc/${w.entityId}`;
    case "DocumentReview":
      return `/dokumenti/${w.entityId}`;
    case "Calibration":
    case "CalibrationMissing":
    case "CalibrationFailed":
      return `/oprema/${w.entityId}`;
    case "Stocktake":
      return `/zalihe/artikli/${w.entityId}`;
    default:
      return "/";
  }
}

/** UI §3: every KPI number leads to the list that makes it (filters in the URL). */
export function kpiLinks(today: string) {
  return {
    activeOrders: "/nalozi?active=true",
    overdueOrders: "/nalozi?overdue=true",
    dueIn7Days: "/nalozi?dueWithinDays=7",
    urgentOrders: "/nalozi?active=true&priority=Hitno",
    goodToday: `/proizvodnja?from=${today}&to=${today}`,
    itemsBelowMinimum: "/zalihe/artikli?hasMinimum=true&stockLevel=IspodMinimuma&stockLevel=Nema",
  } as const;
}
