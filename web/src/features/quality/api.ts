import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Paged, type WithWarnings } from "../../lib/api";
import type { CalibrationStatusAtCheck, InspectionPhase, InspectionResult, NcSource, NcStatus, ProductDecision } from "../../lib/labels";
import { toApiQuery, type ListState } from "../../lib/listParams";
import { lotKey } from "../stock/lotsApi";
import { woKey } from "../workOrders/api";

export const qcKey = ["quality-checks"] as const;
export const ncKey = ["non-conformities"] as const;

export interface EvidenceFile {
  fileId: string;
  originalName: string;
  contentType: string;
  sizeBytes: number;
}

export interface QualityCheck {
  id: string;
  number: string;
  workOrderId: string | null;
  workOrderNumber: string | null;
  materialLotId: string | null;
  lotNumber: string | null;
  phase: InspectionPhase;
  characteristic: string;
  acceptanceCriterion: string | null;
  measuredResult: string | null;
  sampleSize: number | null;
  result: InspectionResult;
  inspectedByEmployeeId: string;
  inspectedByName: string;
  inspectionDate: string;
  measuringEquipmentId: string | null;
  measuringEquipmentCode: string | null;
  measuringEquipmentName: string | null;
  calibrationStatusAtCheck: CalibrationStatusAtCheck;
  evidenceCount: number;
  nonConformityId: string | null;
  nonConformityNumber: string | null;
  createdAt: string;
  isVoided: boolean;
  voidedReason: string | null;
  version: number;
}

export interface CreateQualityCheck {
  workOrderId: string | null;
  materialLotId: string | null;
  phase: InspectionPhase;
  characteristic: string;
  acceptanceCriterion: string | null;
  measuredResult: string | null;
  sampleSize: number | null;
  result: InspectionResult;
  inspectedByEmployeeId: string;
  inspectionDate: string;
  measuringEquipmentId: string | null;
  evidenceFileIds: string[];
}

export interface NonConformity {
  id: string;
  number: string;
  workOrderId: string | null;
  workOrderNumber: string | null;
  materialLotId: string | null;
  lotNumber: string | null;
  supplierId: string | null;
  supplierName: string | null;
  qualityCheckId: string | null;
  qualityCheckNumber: string | null;
  source: NcSource;
  description: string;
  containment: string | null;
  productDecision: ProductDecision;
  rootCause: string | null;
  correctiveAction: string | null;
  actionOwnerEmployeeId: string | null;
  actionOwnerName: string | null;
  dueDate: string | null;
  effectivenessCheckDate: string | null;
  status: NcStatus;
  evidenceCount: number;
  createdAt: string;
  isVoided: boolean;
  voidedReason: string | null;
  version: number;
}

export interface Transition<S extends string> {
  to: S;
  allowed: boolean;
  reason: string | null;
}

export interface NcDetail {
  nc: NonConformity;
  evidence: EvidenceFile[];
  transitions: Transition<NcStatus>[];
}

export interface NcPrefill {
  existingId: string | null;
  existingNumber: string | null;
  qualityCheckId: string;
  qualityCheckNumber: string;
  source: NcSource;
  workOrderId: string | null;
  workOrderNumber: string | null;
  materialLotId: string | null;
  lotNumber: string | null;
  supplierId: string | null;
  supplierName: string | null;
  description: string;
}

export type SaveNc = Pick<NonConformity,
  "workOrderId" | "materialLotId" | "supplierId" | "qualityCheckId" | "source" | "description" | "containment" | "productDecision"
  | "rootCause" | "correctiveAction" | "actionOwnerEmployeeId" | "dueDate" | "effectivenessCheckDate" | "version"> & { evidenceFileIds?: string[] };

/** Checks and NCs change order flags, lot pages and dashboard warnings, so all of them are refetched. */
function useInvalidateQuality() {
  const qc = useQueryClient();
  return () => {
    void qc.invalidateQueries({ queryKey: qcKey });
    void qc.invalidateQueries({ queryKey: ncKey });
    void qc.invalidateQueries({ queryKey: woKey });
    void qc.invalidateQueries({ queryKey: lotKey });
    void qc.invalidateQueries({ queryKey: ["dashboard"] });
    void qc.invalidateQueries({ queryKey: ["history"] });
  };
}

export function useChecks(state: ListState) {
  return useQuery({ queryKey: [...qcKey, "list", state], queryFn: () => api.get<Paged<QualityCheck>>(`/api/quality-checks?${toApiQuery(state)}`), placeholderData: keepPreviousData });
}

export function useCheck(id: string | undefined) {
  return useQuery({ queryKey: [...qcKey, id], queryFn: () => api.get<{ check: QualityCheck; evidence: EvidenceFile[] }>(`/api/quality-checks/${id}`), enabled: !!id });
}

export function useWorkOrderChecks(workOrderId: string) {
  return useQuery({ queryKey: [...qcKey, "work-order", workOrderId], queryFn: () => api.get<QualityCheck[]>(`/api/work-orders/${workOrderId}/quality-checks`) });
}

export function useLotChecks(lotId: string) {
  return useQuery({ queryKey: [...qcKey, "lot", lotId], queryFn: () => api.get<QualityCheck[]>(`/api/lots/${lotId}/quality-checks`) });
}

export function useCreateCheck() {
  const invalidate = useInvalidateQuality();
  return useMutation({ mutationFn: (body: CreateQualityCheck) => api.post<WithWarnings<QualityCheck>>("/api/quality-checks", body), onSuccess: invalidate });
}

export function useVoidCheck() {
  const invalidate = useInvalidateQuality();
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; reason: string; version: number }) => api.post<QualityCheck>(`/api/quality-checks/${id}/void`, body),
    onSuccess: invalidate,
  });
}

export function useCheckEvidence(checkId: string) {
  const invalidate = useInvalidateQuality();
  return {
    add: useMutation({ mutationFn: (fileId: string) => api.post<void>(`/api/quality-checks/${checkId}/evidence`, { fileId }), onSuccess: invalidate }),
    remove: useMutation({ mutationFn: (fileId: string) => api.post<void>(`/api/quality-checks/${checkId}/evidence/${fileId}/remove`), onSuccess: invalidate }),
  };
}

export function useNcs(state: ListState) {
  return useQuery({ queryKey: [...ncKey, "list", state], queryFn: () => api.get<Paged<NonConformity>>(`/api/non-conformities?${toApiQuery(state)}`), placeholderData: keepPreviousData });
}

export function useNc(id: string | undefined) {
  return useQuery({ queryKey: [...ncKey, id], queryFn: () => api.get<NcDetail>(`/api/non-conformities/${id}`), enabled: !!id });
}

export function useNcPrefill(qualityCheckId: string | null) {
  return useQuery({
    queryKey: [...ncKey, "prefill", qualityCheckId],
    queryFn: () => api.get<NcPrefill>(`/api/non-conformities/prefill?qualityCheckId=${qualityCheckId}`),
    enabled: !!qualityCheckId,
  });
}

export function useWorkOrderNcs(workOrderId: string) {
  return useQuery({ queryKey: [...ncKey, "work-order", workOrderId], queryFn: () => api.get<NonConformity[]>(`/api/work-orders/${workOrderId}/non-conformities`) });
}

export function useLotNcs(lotId: string) {
  return useQuery({ queryKey: [...ncKey, "lot", lotId], queryFn: () => api.get<NonConformity[]>(`/api/lots/${lotId}/non-conformities`) });
}

export function useSaveNc(id: string | undefined) {
  const invalidate = useInvalidateQuality();
  return useMutation({
    mutationFn: (body: SaveNc) => (id ? api.put<NonConformity>(`/api/non-conformities/${id}`, body) : api.post<NonConformity>("/api/non-conformities", body)),
    onSuccess: invalidate,
  });
}

export function useNcStatus(id: string) {
  const invalidate = useInvalidateQuality();
  return useMutation({
    mutationFn: (body: { status: NcStatus; version: number }) => api.post<NonConformity>(`/api/non-conformities/${id}/status`, body),
    onSuccess: invalidate,
  });
}

export function useVoidNc() {
  const invalidate = useInvalidateQuality();
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; reason: string; version: number }) => api.post<NonConformity>(`/api/non-conformities/${id}/void`, body),
    onSuccess: invalidate,
  });
}

export function useNcEvidence(ncId: string) {
  const invalidate = useInvalidateQuality();
  return {
    add: useMutation({ mutationFn: (fileId: string) => api.post<void>(`/api/non-conformities/${ncId}/evidence`, { fileId }), onSuccess: invalidate }),
    remove: useMutation({ mutationFn: (fileId: string) => api.post<void>(`/api/non-conformities/${ncId}/evidence/${fileId}/remove`), onSuccess: invalidate }),
  };
}
