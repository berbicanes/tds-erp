import { api } from "./api";
import { formatQuantity } from "./format";

/** DATA_MODEL §14 — mirrored for instant feedback; the server checks again (incl. content). */
export const ALLOWED_EXTENSIONS = [".pdf", ".dxf", ".dwg", ".step", ".stp", ".png", ".jpg", ".jpeg", ".xlsx", ".docx"];
export const MAX_FILE_BYTES = 25 * 1024 * 1024;

export interface UploadedFile {
  id: string;
  originalName: string;
  contentType: string;
  sizeBytes: number;
}

export function checkFile(file: { name: string; size: number }): string | null {
  const dot = file.name.lastIndexOf(".");
  const ext = dot >= 0 ? file.name.slice(dot).toLowerCase() : "";
  if (!ALLOWED_EXTENSIONS.includes(ext)) return `Vrsta fajla nije dozvoljena. Dozvoljeno: ${ALLOWED_EXTENSIONS.join(", ")}.`;
  if (file.size === 0) return "Fajl je prazan.";
  if (file.size > MAX_FILE_BYTES) return "Fajl je veći od 25 MB.";
  return null;
}

export function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${formatQuantity(Math.round(bytes / 102.4) / 10)} KB`;
  return `${formatQuantity(Math.round(bytes / (1024 * 102.4)) / 10)} MB`;
}

export function uploadFile(file: File): Promise<UploadedFile> {
  const form = new FormData();
  form.append("file", file);
  return api.post<UploadedFile>("/api/files", form);
}

export const fileUrl = (id: string) => `/api/files/${id}`;
