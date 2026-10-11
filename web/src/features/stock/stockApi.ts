import { keepPreviousData, useQuery } from "@tanstack/react-query";
import { api, type Paged } from "../../lib/api";
import type { StockLevel, Unit } from "../../lib/labels";
import { toApiQuery, type ListState } from "../../lib/listParams";

export interface ItemStock {
  materialItemId: string;
  name: string;
  grade: string | null;
  thicknessMm: number | null;
  unit: Unit;
  minimumStock: number | null;
  onHand: number;
  reserved: number;
  available: number;
  /** null = not known: lots without quantity and the known ones are not OK (DOMAIN §2.4) → "—". */
  stockLevel: StockLevel | null;
  onHoldQty: number;
  /** Lots with unknown stock (DOMAIN §2.6), not part of the sums. */
  unknownLotCount: number;
  isActive: boolean;
}

export function useItemStock(state: ListState) {
  return useQuery({
    queryKey: ["stock", "items", state],
    queryFn: () => api.get<Paged<ItemStock>>(`/api/stock/items?${toApiQuery(state)}`),
    placeholderData: keepPreviousData,
  });
}
