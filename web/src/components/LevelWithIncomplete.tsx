import { Badge } from "./Badge";
import { labelOf, stockLevel, type StockLevel } from "../lib/labels";
import { incompleteLabel } from "../lib/material";

/** Stock level badge plus "Nepotpuno: X lotova bez količine" when some lots have unknown stock (DOMAIN §2.6). */
export function LevelWithIncomplete({ item }: { item: { stockLevel: StockLevel; unknownLotCount: number } }) {
  const incomplete = incompleteLabel(item.unknownLotCount);
  return (
    <span className="inline-flex flex-wrap items-center gap-1">
      <Badge label={labelOf(stockLevel, item.stockLevel)} />
      {incomplete && <span className="rounded bg-amber-100 px-1.5 py-0.5 text-xs text-amber-900">{incomplete}</span>}
    </span>
  );
}
