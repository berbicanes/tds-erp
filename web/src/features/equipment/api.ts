import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Paged } from "../../lib/api";
import type { CalibrationResult, CalibrationStatusAtCheck, EquipmentKind } from "../../lib/labels";
import { toApiQuery, type ListState } from "../../lib/listParams";

export interface Equipment {
  id: string;
  code: string;
  name: string;
  kind: EquipmentKind;
  manufacturer: string | null;
  serialNumber: string | null;
  calibrationIntervalMonths: number | null;
  /** Derived from the latest non-voided calibration. */
  lastCalibrationDate: string | null;
  nextCalibrationDate: string | null;
  lastCalibrationResult: CalibrationResult | null;
  calibrationStatus: CalibrationStatusAtCheck | null;
  isActive: boolean;
  version: number;
}

export interface Calibration {
  id: string;
  calibrationDate: string;
  nextCalibrationDate: string | null;
  performedBy: string;
  result: CalibrationResult;
  certificateFileId: string | null;
  certificateFileName: string | null;
  note: string | null;
  createdByName: string | null;
  isVoided: boolean;
  voidedReason: string | null;
  version: number;
}

export interface EquipmentOption {
  id: string;
  code: string;
  name: string;
  kind: EquipmentKind;
  calibrationStatus: CalibrationStatusAtCheck | null;
}

export type SaveEquipment = Pick<Equipment, "code" | "name" | "kind" | "manufacturer" | "serialNumber" | "calibrationIntervalMonths" | "isActive" | "version">;

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

export function useCalibrations(equipmentId: string | undefined) {
  return useQuery({
    queryKey: [...key, equipmentId, "calibrations"],
    queryFn: () => api.get<Calibration[]>(`/api/equipment/${equipmentId}/calibrations`),
    enabled: !!equipmentId,
  });
}

export function useRecordCalibration(equipmentId: string) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: { calibrationDate: string; nextCalibrationDate: string | null; performedBy: string; result: CalibrationResult; certificateFileId: string | null; note: string | null }) =>
      api.post<Calibration>(`/api/equipment/${equipmentId}/calibrations`, body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: key });
      void qc.invalidateQueries({ queryKey: ["dashboard"] });
    },
  });
}

export function useVoidCalibration() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; reason: string; version: number }) => api.post<Calibration>(`/api/equipment-calibrations/${id}/void`, body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: key });
      void qc.invalidateQueries({ queryKey: ["dashboard"] });
    },
  });
}
