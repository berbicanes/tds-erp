import { useQuery } from "@tanstack/react-query";
import { api } from "./api";

export type JsonValue = string | number | boolean | null | JsonValue[];

export interface HistoryChange {
  field: string;
  old: JsonValue;
  new: JsonValue;
}

export interface HistoryEntry {
  id: string;
  at: string;
  userName: string | null;
  action: "Create" | "Update" | "Void";
  entityType: string;
  entityId: string;
  recordLabel: string;
  reason: string | null;
  changes: HistoryChange[];
}

export type HistoryScope = "work-order" | "lot" | "non-conformity" | "document";

export function useHistory(scope: HistoryScope, id: string) {
  return useQuery({ queryKey: ["history", scope, id], queryFn: () => api.get<HistoryEntry[]>(`/api/history/${scope}/${id}`) });
}
