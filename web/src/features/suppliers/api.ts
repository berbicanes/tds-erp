import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Paged } from "../../lib/api";
import type { SupplierApprovalStatus } from "../../lib/labels";
import { toApiQuery, type ListState } from "../../lib/listParams";

export interface Supplier {
  id: string;
  name: string;
  materialOrService: string | null;
  approvalStatus: SupplierApprovalStatus;
  initialEvaluationDate: string | null;
  nextEvaluationDate: string | null;
  evaluationCriteria: string | null;
  responsibleEmployeeId: string | null;
  responsibleEmployeeName: string | null;
  notes: string | null;
  version: number;
}

export interface SupplierOption {
  id: string;
  name: string;
  approvalStatus: SupplierApprovalStatus;
}

export type SaveSupplier = Omit<Supplier, "id" | "responsibleEmployeeName">;

const key = ["suppliers"] as const;

export function useSuppliers(state: ListState) {
  return useQuery({
    queryKey: [...key, "list", state],
    queryFn: () => api.get<Paged<Supplier>>(`/api/suppliers?${toApiQuery(state)}`),
    placeholderData: keepPreviousData,
  });
}

export function useSupplier(id: string | undefined) {
  return useQuery({ queryKey: [...key, id], queryFn: () => api.get<Supplier>(`/api/suppliers/${id}`), enabled: !!id });
}

export function useSupplierOptions() {
  return useQuery({ queryKey: [...key, "options"], queryFn: () => api.get<SupplierOption[]>("/api/suppliers/options"), staleTime: 60_000 });
}

export function useSaveSupplier(id: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: SaveSupplier) => (id ? api.put<Supplier>(`/api/suppliers/${id}`, body) : api.post<Supplier>("/api/suppliers", body)),
    onSuccess: () => qc.invalidateQueries({ queryKey: key }),
  });
}
