import { parseDecimal } from "../../lib/format";
import type { WorkOrderStatus } from "../../lib/labels";

/** DOMAIN §5.1: statuses in which production can be recorded. */
export const PRODUCTION_STATUSES: WorkOrderStatus[] = ["Spreman", "Laser", "Presanje", "Kontrola", "Doraditi"];

export interface QuickEntryInput {
  orderId: string | null;
  operatorId: string | null;
  equipmentIds: string[];
  lotId: string | null;
  laser: string;
  pressed: string;
  good: string;
  scrap: string;
  hours: string;
  consumed: string;
}

/** Client check before sending (the server checks DOMAIN §5 again). Messages say what is wrong (UI §9). */
export function quickEntryProblems(v: QuickEntryInput): string[] {
  const p: string[] = [];
  const n = (s: string) => Number(s) || 0;
  if (!v.orderId) p.push("Odaberi nalog.");
  if (!v.operatorId) p.push("Odaberi operatera.");
  if (v.equipmentIds.length === 0) p.push("Odaberi barem jednu mašinu.");
  if (n(v.laser) + n(v.pressed) + n(v.good) + n(v.scrap) === 0) p.push("Unesi barem jednu količinu: laser, presa, gotovo ili škart.");
  const consumed = v.consumed.trim() ? parseDecimal(v.consumed) : null;
  if (v.consumed.trim() && (consumed === null || consumed <= 0)) p.push("Utrošak mora biti broj veći od 0 ili prazan.");
  if ((n(v.laser) > 0 || (consumed ?? 0) > 0) && !v.lotId) p.push("Odaberi lot materijala — obavezan je kad se reže ili troši materijal.");
  const hours = v.hours.trim() ? parseDecimal(v.hours) : null;
  if (hours === null) p.push("Unesi radne sate.");
  else if (hours < 0 || hours > 24) p.push("Radni sati moraju biti između 0 i 24.");
  return p;
}
