import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type WithWarnings } from "../../lib/api";
import type { ReceiptDecision, Unit } from "../../lib/labels";
import { woKey } from "./api";

export interface WorkOrderMaterial {
  id: string;
  materialLotId: string;
  lotNumber: string;
  itemName: string;
  itemGrade: string | null;
  itemThicknessMm: number | null;
  unit: Unit;
  receiptDecision: ReceiptDecision;
  reservedQty: number;
  consumedQty: number;
  effectiveReservation: number;
  lotOnHand: number;
  lotAvailable: number;
  version: number;
}

export function useWorkOrderMaterials(workOrderId: string) {
  return useQuery({
    queryKey: [...woKey, workOrderId, "materials"],
    queryFn: () => api.get<WorkOrderMaterial[]>(`/api/work-orders/${workOrderId}/materials`),
  });
}

function useInvalidate() {
  const qc = useQueryClient();
  return () => {
    void qc.invalidateQueries({ queryKey: woKey });
    void qc.invalidateQueries({ queryKey: ["stock"] });
    void qc.invalidateQueries({ queryKey: ["lots"] });
  };
}

export function useLinkLot(workOrderId: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: { materialLotId: string; reservedQty: number }) =>
      api.post<WithWarnings<WorkOrderMaterial>>(`/api/work-orders/${workOrderId}/materials`, body),
    onSuccess: invalidate,
  });
}

export function useSetReservation(workOrderId: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ linkId, ...body }: { linkId: string; reservedQty: number; version: number }) =>
      api.put<WithWarnings<WorkOrderMaterial>>(`/api/work-orders/${workOrderId}/materials/${linkId}`, body),
    onSuccess: invalidate,
  });
}
