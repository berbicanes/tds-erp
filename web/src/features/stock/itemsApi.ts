import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../lib/api";
import type { Unit } from "../../lib/labels";

export interface MaterialItem {
  id: string;
  name: string;
  grade: string | null;
  thicknessMm: number | null;
  unit: Unit;
  minimumStock: number | null;
  nextStocktakeDate: string | null;
  isActive: boolean;
  version: number;
}

export interface MaterialItemOption {
  id: string;
  name: string;
  grade: string | null;
  thicknessMm: number | null;
  unit: Unit;
}

export type SaveMaterialItem = Omit<MaterialItem, "id">;

export const itemKey = ["material-items"] as const;

export function useMaterialItem(id: string | undefined) {
  return useQuery({ queryKey: [...itemKey, id], queryFn: () => api.get<MaterialItem>(`/api/material-items/${id}`), enabled: !!id });
}

export function useMaterialItemOptions() {
  return useQuery({ queryKey: [...itemKey, "options"], queryFn: () => api.get<MaterialItemOption[]>("/api/material-items/options"), staleTime: 60_000 });
}

export function useSaveMaterialItem(id: string | undefined) {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (body: SaveMaterialItem) =>
      id ? api.put<MaterialItem>(`/api/material-items/${id}`, body) : api.post<MaterialItem>("/api/material-items", body),
    onSuccess: () => {
      void qc.invalidateQueries({ queryKey: itemKey });
      void qc.invalidateQueries({ queryKey: ["stock"] });
    },
  });
}
