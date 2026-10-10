import { EMPTY, formatQuantity } from "./format";

/** "Lim crni S235 3 mm" — one readable name for an item wherever it appears. */
export function itemTitle(item: { name: string; grade: string | null; thicknessMm: number | null }): string {
  return [item.name, item.grade, item.thicknessMm != null ? `${formatQuantity(item.thicknessMm)} mm` : null].filter(Boolean).join(" ");
}

/** Quantity with unit, "—" when unknown. */
export function qtyWithUnit(value: number | null | undefined, unit: string): string {
  return value == null ? EMPTY : `${formatQuantity(value)} ${unit}`;
}
