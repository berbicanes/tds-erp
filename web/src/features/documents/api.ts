import { keepPreviousData, useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api, type Paged } from "../../lib/api";
import type { DocumentKind, DocumentStatus } from "../../lib/labels";
import { toApiQuery, type ListState } from "../../lib/listParams";
import type { Transition } from "../quality/api";

export const docKey = ["documents"] as const;

export interface ControlledDocument {
  id: string;
  title: string;
  code: string;
  kind: DocumentKind;
  /** DATA_MODEL "Version" (e.g. "02"); `version` is the concurrency token. */
  docVersion: string;
  status: DocumentStatus;
  ownerEmployeeId: string | null;
  ownerName: string | null;
  approvalDate: string | null;
  effectiveDate: string | null;
  nextReviewDate: string | null;
  changeDescription: string | null;
  fileId: string | null;
  fileName: string | null;
  isVoided: boolean;
  voidedReason: string | null;
  version: number;
}

export interface DocumentDetail {
  document: ControlledDocument;
  transitions: Transition<DocumentStatus>[];
  versions: ControlledDocument[];
}

export type SaveDocument = Pick<ControlledDocument,
  "title" | "code" | "kind" | "docVersion" | "ownerEmployeeId" | "approvalDate" | "effectiveDate" | "nextReviewDate" | "changeDescription" | "fileId" | "version">;

function useInvalidate() {
  const qc = useQueryClient();
  return () => {
    void qc.invalidateQueries({ queryKey: docKey });
    void qc.invalidateQueries({ queryKey: ["dashboard"] });
    void qc.invalidateQueries({ queryKey: ["history"] });
  };
}

export function useDocuments(state: ListState) {
  return useQuery({ queryKey: [...docKey, "list", state], queryFn: () => api.get<Paged<ControlledDocument>>(`/api/documents?${toApiQuery(state)}`), placeholderData: keepPreviousData });
}

export function useDocument(id: string | undefined) {
  return useQuery({ queryKey: [...docKey, id], queryFn: () => api.get<DocumentDetail>(`/api/documents/${id}`), enabled: !!id });
}

export function useSaveDocument(id: string | undefined) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: SaveDocument) => (id ? api.put<ControlledDocument>(`/api/documents/${id}`, body) : api.post<ControlledDocument>("/api/documents", body)),
    onSuccess: invalidate,
  });
}

export function useDocumentStatus(id: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: { status: DocumentStatus; approvalDate?: string | null; version: number }) => api.post<ControlledDocument>(`/api/documents/${id}/status`, body),
    onSuccess: invalidate,
  });
}

export function useNextReview(id: string) {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: (body: { nextReviewDate: string | null; version: number }) => api.post<ControlledDocument>(`/api/documents/${id}/next-review`, body),
    onSuccess: invalidate,
  });
}

export function useNewVersion(id: string) {
  const invalidate = useInvalidate();
  return useMutation({ mutationFn: (docVersion: string) => api.post<ControlledDocument>(`/api/documents/${id}/new-version`, { docVersion }), onSuccess: invalidate });
}

export function useVoidDocument() {
  const invalidate = useInvalidate();
  return useMutation({
    mutationFn: ({ id, ...body }: { id: string; reason: string; version: number }) => api.post<ControlledDocument>(`/api/documents/${id}/void`, body),
    onSuccess: invalidate,
  });
}

/** Proposal for the next version label: "01" → "02", "1.3" → "1.4", otherwise empty. */
export function nextVersionLabel(current: string): string {
  const m = /^(.*?)(\d+)$/.exec(current.trim());
  if (!m) return "";
  const n = String(Number(m[2]) + 1).padStart(m[2]!.length, "0");
  return `${m[1]}${n}`;
}
