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
  stockLevel: StockLevel;
  onHoldQty: number;
  isActive: boolean;
}

export function useItemStock(state: ListState) {
  return useQuery({
    queryKey: ["stock", "items", state],
    queryFn: () => api.get<Paged<ItemStock>>(`/api/stock/items?${toApiQuery(state)}`),
    placeholderData: keepPreviousData,
  });
}
