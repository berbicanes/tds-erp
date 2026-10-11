import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Paged, type WithWarnings } from "../../lib/api";
import type { Unit } from "../../lib/labels";
import { toApiQuery, type ListState } from "../../lib/listParams";
import { woKey } from "../workOrders/api";

export interface ProductionEntry {
  id: string;
  number: string;
  date: string;
  workOrderId: string;
  workOrderNumber: string;
  customer: string;
  operatorEmployeeId: string;
  operatorName: string;
  materialLotId: string | null;
  lotNumber: string | null;
  lotUnit: Unit | null;
  laserCutQty: number;
  pressedQty: number;
  goodQty: number;
  scrapQty: number;
  laborHours: number;
  materialConsumed: number | null;
  note: string | null;
  equipmentIds: string[];
  equipmentCodes: string[];
  createdBy: string | null;
  createdByName: string | null;
  createdAt: string;
  isVoided: boolean;
  voidedReason: string | null;
  version: number;
}

export interface SaveProductionEntry {
  workOrderId: string;
  date: string;
  operatorEmployeeId: string;
  materialLotId: string | null;
  equipmentIds: string[];
  laserCutQty: number;
  pressedQty: number;
  goodQty: number;
  scrapQty: number;
  /** Required: null is sent as-is and rejected by the server, never turned into 0 (golden rule 6). */
  laborHours: number | null;
  materialConsumed: number | null;
  note: string | null;
  version: number;
}

export interface ProductionTotals {
  laserCutQty: number;
  pressedQty: number;
  goodQty: number;
  scrapQty: number;
  laborHours: number;
  entries: number;
}

export const peKey = ["production-entries"] as const;

export function useProductionEntries(state: ListState) {
  return useQuery({
    queryKey: [...peKey, "list", state],
    queryFn: () => api.get<Paged<ProductionEntry>>(`/api/production-entries?${toApiQuery(state)}`),
    placeholderData: keepPreviousData,
  });
}

export function useProductionEntry(id: string | undefined) {
  return useQuery({ queryKey: [...peKey, id], queryFn: () => api.get<ProductionEntry>(`/api/production-entries/${id}`), enabled: !!id });
}

export function useWorkOrderProduction(workOrderId: string) {
  return useQuery({
    queryKey: [...woKey, workOrderId, "production"],
    queryFn: () => api.get<{ items: ProductionEntry[]; totals: ProductionTotals }>(`/api/work-orders/${workOrderId}/production-entries`),
  });
}

/** Entries change stock, reservations and the order's derived values. */
function useInvalidateAll() {
  const qc = useQueryClient();
  return () => {
    for (const key of [peKey, woKey, ["lots"], ["stock"], ["material-items"]]) void qc.invalidateQueries({ queryKey: key });
  };
}

export function useSaveProductionEntry(id: string | undefined) {
  const invalidate = useInvalidateAll();
  return useMutation({
    mutationFn: (body: SaveProductionEntry) =>
      id
        ? api.put<WithWarnings<ProductionEntry>>(`/api/production-entries/${id}`, body)
        : api.post<WithWarnings<ProductionEntry>>("/api/production-entries", body),
    onSuccess: invalidate,
  });
}

export function useVoidProductionEntry() {
  const invalidate = useInvalidateAll();
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; reason: string; version: number }) => api.post<ProductionEntry>(`/api/production-entries/${id}/void`, body),
    onSuccess: invalidate,
  });
}
