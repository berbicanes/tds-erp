import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Paged } from "../../lib/api";
import type { EquipmentKind } from "../../lib/labels";
import { toApiQuery, type ListState } from "../../lib/listParams";

export interface Equipment {
  id: string;
  code: string;
  name: string;
  kind: EquipmentKind;
  manufacturer: string | null;
  serialNumber: string | null;
  calibrationIntervalMonths: number | null;
  lastCalibrationDate: string | null;
  nextCalibrationDate: string | null;
  calibrationCertificateFileId: string | null;
  isActive: boolean;
  version: number;
}

export interface EquipmentOption {
  id: string;
  code: string;
  name: string;
  kind: EquipmentKind;
}

export type SaveEquipment = Omit<Equipment, "id" | "calibrationCertificateFileId">;

const key = ["equipment"] as const;

export function useEquipmentList(state: ListState) {
  return useQuery({
    queryKey: [...key, "list", state],
    queryFn: () => api.get<Paged<Equipment>>(`/api/equipment?${toApiQuery(state)}`),
    placeholderData: keepPreviousData,
  });
}

export function useEquipment(id: string | undefined) {
  return useQuery({ queryKey: [...key, id], queryFn: () => api.get<Equipment>(`/api/equipment/${id}`), enabled: !!id });
}

export function useEquipmentOptions(kind?: EquipmentKind) {
  return useQuery({
    queryKey: [...key, "options", kind ?? "all"],
    queryFn: () => api.get<EquipmentOption[]>(`/api/equipment/options${kind ? `?kind=${kind}` : ""}`),
    staleTime: 5 * 60_000,
  });
}

export function useSaveEquipment(id: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: SaveEquipment) => (id ? api.put<Equipment>(`/api/equipment/${id}`, body) : api.post<Equipment>("/api/equipment", body)),
    onSuccess: () => qc.invalidateQueries({ queryKey: key }),
  });
}
