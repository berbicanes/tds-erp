import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../lib/api";
import type { InspectionPhase, InspectionResult } from "../../lib/labels";
import { woKey } from "../workOrders/api";

export interface QualityCheck {
  id: string;
  number: string;
  workOrderId: string | null;
  phase: InspectionPhase;
  characteristic: string;
  acceptanceCriterion: string | null;
  measuredResult: string | null;
  sampleSize: number | null;
  result: InspectionResult;
  inspectedByEmployeeId: string;
  inspectedByName: string;
  inspectionDate: string;
  isVoided: boolean;
  voidedReason: string | null;
  version: number;
}

export interface CreateQualityCheck {
  workOrderId: string;
  phase: InspectionPhase;
  characteristic: string;
  acceptanceCriterion: string | null;
  measuredResult: string | null;
  sampleSize: number | null;
  result: InspectionResult;
  inspectedByEmployeeId: string;
  inspectionDate: string;
}

export function useWorkOrderChecks(workOrderId: string) {
  return useQuery({
    queryKey: [...woKey, workOrderId, "quality-checks"],
    queryFn: () => api.get<QualityCheck[]>(`/api/work-orders/${workOrderId}/quality-checks`),
  });
}

/** Checks change the derived flags of the order (DOMAIN §3), so the order is refetched too. */
export function useCreateCheck() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: CreateQualityCheck) => api.post<QualityCheck>("/api/quality-checks", body),
    onSuccess: () => qc.invalidateQueries({ queryKey: woKey }),
  });
}

export function useVoidCheck() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; reason: string; version: number }) => api.post<QualityCheck>(`/api/quality-checks/${id}/void`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: woKey }),
  });
}
