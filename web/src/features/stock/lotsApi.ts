import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Paged, type WithWarnings } from "../../lib/api";
import type { ReceiptDecision, Unit } from "../../lib/labels";
import { toApiQuery, type ListState } from "../../lib/listParams";
import { itemKey } from "./itemsApi";

export interface Lot {
  id: string;
  lotNumber: string;
  materialItemId: string;
  itemName: string;
  itemGrade: string | null;
  itemThicknessMm: number | null;
  unit: Unit;
  supplierId: string;
  supplierName: string;
  receivedDate: string;
  dimensionsDescription: string | null;
  /** null = unknown (lot imported from Notion). */
  receivedQty: number | null;
  onHand: number;
  receiptDecision: ReceiptDecision;
  inspectedByEmployeeId: string | null;
  inspectedByName: string | null;
  storageLocation: string | null;
  certificateFileId: string | null;
  certificateFileName: string | null;
  isVoided: boolean;
  voidedReason: string | null;
  version: number;
}

export interface ReceiveLot {
  lotNumber: string;
  materialItemId: string;
  supplierId: string;
  receivedDate: string;
  receivedQty: number;
  dimensionsDescription: string | null;
  storageLocation: string | null;
}

/** DOMAIN §2.5a — mirrors MaterialLot.AllowedDecisions (server is the authority). */
export const allowedDecisions: Record<ReceiptDecision, ReceiptDecision[]> = {
  NaKontroli: ["Prihvacen", "Karantin", "Odbijen"],
  Karantin: ["Prihvacen", "Odbijen"],
  Prihvacen: ["Karantin"],
  Odbijen: [],
};

export const lotKey = ["lots"] as const;

export function useLots(state: ListState, extra = "") {
  return useQuery({
    queryKey: [...lotKey, "list", state, extra],
    queryFn: () => api.get<Paged<Lot>>(`/api/lots?${toApiQuery(state)}${extra}`),
    placeholderData: keepPreviousData,
  });
}

export function useLot(id: string | undefined) {
  return useQuery({ queryKey: [...lotKey, id], queryFn: () => api.get<Lot>(`/api/lots/${id}`), enabled: !!id });
}

function useInvalidateStock() {
  const qc = useQueryClient();
  return () => {
    void qc.invalidateQueries({ queryKey: lotKey });
    void qc.invalidateQueries({ queryKey: itemKey });
    void qc.invalidateQueries({ queryKey: ["stock"] });
  };
}

export function useReceiveLot() {
  const invalidate = useInvalidateStock();
  return useMutation({ mutationFn: (body: ReceiveLot) => api.post<WithWarnings<Lot>>("/api/lots", body), onSuccess: invalidate });
}

export function useUpdateLot(id: string) {
  const invalidate = useInvalidateStock();
  return useMutation({
    mutationFn: (body: { lotNumber: string; supplierId: string; dimensionsDescription: string | null; storageLocation: string | null; version: number }) =>
      api.put<Lot>(`/api/lots/${id}`, body),
    onSuccess: invalidate,
  });
}

export function useDecideLot(id: string) {
  const invalidate = useInvalidateStock();
  return useMutation({
    mutationFn: (body: { decision: ReceiptDecision; inspectedByEmployeeId: string | null; reason: string | null; version: number }) =>
      api.post<WithWarnings<Lot>>(`/api/lots/${id}/decision`, body),
    onSuccess: invalidate,
  });
}

export function useVoidLot(id: string) {
  const invalidate = useInvalidateStock();
  return useMutation({
    mutationFn: (body: { reason: string; version: number }) => api.post<Lot>(`/api/lots/${id}/void`, body),
    onSuccess: invalidate,
  });
}
