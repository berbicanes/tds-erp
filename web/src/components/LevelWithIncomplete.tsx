import { Badge } from "./Badge";
import { EMPTY } from "../lib/format";
import { labelOf, stockLevel, type StockLevel } from "../lib/labels";
import { incompleteLabel } from "../lib/material";

/** Text of the level for CSV: "—" when the level is not known (DOMAIN §2.4), plus the "Nepotpuno" mark. */
export function levelText(item: { stockLevel: StockLevel | null; unknownLotCount: number }): string {
  return [item.stockLevel ? labelOf(stockLevel, item.stockLevel).text : EMPTY, incompleteLabel(item.unknownLotCount)].filter(Boolean).join(" · ");
}

/**
 * Stock level badge plus "Nepotpuno: X lotova bez količine" when some lots have unknown stock (DOMAIN §2.6).
 * The level itself is "—" when it is not known (DOMAIN §2.4).
 */
export function LevelWithIncomplete({ item }: { item: { stockLevel: StockLevel | null; unknownLotCount: number } }) {
  const incomplete = incompleteLabel(item.unknownLotCount);
  return (
    <span className="inline-flex flex-wrap items-center gap-1">
      {item.stockLevel ? <Badge label={labelOf(stockLevel, item.stockLevel)} /> : <span>{EMPTY}</span>}
      {incomplete && <span className="rounded bg-amber-100 px-1.5 py-0.5 text-xs text-amber-900">{incomplete}</span>}
    </span>
  );
}
