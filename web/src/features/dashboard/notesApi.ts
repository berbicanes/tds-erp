import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api } from "../../lib/api";
import { dashboardKey } from "./api";

export interface NoteDto {
  id: string;
  text: string;
  isDone: boolean;
  doneAt: string | null;
  createdAt: string;
  version: number;
}

export function useAddNote() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (text: string) => api.post<NoteDto>("/api/notes", { text }),
    onSuccess: () => qc.invalidateQueries({ queryKey: dashboardKey }),
  });
}

export function useSetNoteDone() {
  const qc = useQueryClient();
  return useMutation({
    mutationFn: (n: { id: string; isDone: boolean; version: number }) => api.post<NoteDto>(`/api/notes/${n.id}/done`, { isDone: n.isDone, version: n.version }),
    onSuccess: () => qc.invalidateQueries({ queryKey: dashboardKey }),
  });
}
