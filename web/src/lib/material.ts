import { EMPTY, formatQuantity } from "./format";

/** "Lim crni S235 3 mm" — one readable name for an item wherever it appears. */
export function itemTitle(item: { name: string; grade: string | null; thicknessMm: number | null }): string {
  return [item.name, item.grade, item.thicknessMm != null ? `${formatQuantity(item.thicknessMm)} mm` : null].filter(Boolean).join(" ");
}

/** DOMAIN §2.6: "Nepotpuno: 2 lota bez količine" (Bosnian plural); null when every lot has a known stock. */
export function incompleteLabel(unknownLots: number): string | null {
  if (unknownLots <= 0) return null;
  const tens = unknownLots % 100;
  const ones = unknownLots % 10;
  const noun = ones === 1 && tens !== 11 ? "lot" : ones >= 2 && ones <= 4 && (tens < 12 || tens > 14) ? "lota" : "lotova";
  return `Nepotpuno: ${unknownLots} ${noun} bez količine`;
}

/** Quantity with unit, "—" when unknown. */
export function qtyWithUnit(value: number | null | undefined, unit: string): string {
  return value == null ? EMPTY : `${formatQuantity(value)} ${unit}`;
}
