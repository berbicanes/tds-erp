import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Paged } from "../../lib/api";
import type { Priority, QualityApproval, WorkOrderStatus } from "../../lib/labels";
import { toApiQuery, type ListState } from "../../lib/listParams";

export interface WorkOrder {
  id: string;
  number: string;
  customer: string;
  productDescription: string;
  orderedQty: number;
  dueDate: string;
  releaseDate: string | null;
  status: WorkOrderStatus;
  priority: Priority;
  qualityApproval: QualityApproval;
  drawingNumber: string | null;
  drawingRevision: string | null;
  drawingFileId: string | null;
  drawingFileName: string | null;
  responsibleEmployeeId: string | null;
  responsibleEmployeeName: string | null;
  notes: string | null;
  completedQty: number;
  laserFirstPieceApproved: boolean;
  pressFirstPieceApproved: boolean;
  finalInspectionPassed: boolean;
  readyForShipment: boolean;
  progressPercent: number;
  isOverdue: boolean;
  version: number;
}

export interface WorkOrderOption {
  id: string;
  number: string;
  customer: string;
  productDescription: string;
  status: WorkOrderStatus;
}

export interface SaveWorkOrder {
  customer: string;
  productDescription: string;
  orderedQty: number;
  dueDate: string;
  releaseDate: string | null;
  priority: Priority;
  drawingNumber: string | null;
  drawingRevision: string | null;
  drawingFileId: string | null;
  responsibleEmployeeId: string | null;
  notes: string | null;
  version: number;
}

export const woKey = ["work-orders"] as const;

export function useWorkOrders(state: ListState) {
  return useQuery({
    queryKey: [...woKey, "list", state],
    queryFn: () => api.get<Paged<WorkOrder>>(`/api/work-orders?${toApiQuery(state)}`),
    placeholderData: keepPreviousData,
  });
}

export function useWorkOrder(id: string | undefined) {
  return useQuery({ queryKey: [...woKey, id], queryFn: () => api.get<WorkOrder>(`/api/work-orders/${id}`), enabled: !!id });
}

export function useWorkOrderOptions() {
  return useQuery({ queryKey: [...woKey, "options"], queryFn: () => api.get<WorkOrderOption[]>("/api/work-orders/options"), staleTime: 30_000 });
}

export function useSaveWorkOrder(id: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: SaveWorkOrder) => (id ? api.put<WorkOrder>(`/api/work-orders/${id}`, body) : api.post<WorkOrder>("/api/work-orders", body)),
    onSuccess: () => qc.invalidateQueries({ queryKey: woKey }),
  });
}

export function useSetQualityApproval(id: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { qualityApproval: QualityApproval; version: number }) => api.post<WorkOrder>(`/api/work-orders/${id}/quality-approval`, body),
    onSuccess: () => qc.invalidateQueries({ queryKey: woKey }),
  });
}
