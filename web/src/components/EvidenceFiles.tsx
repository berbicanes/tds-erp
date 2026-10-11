import { useState } from "react";
import type { UseMutationResult } from "@tanstack/react-query";
import { ConfirmDialog } from "./ConfirmDialog";
import { ErrorBanner } from "./ErrorBanner";
import { FilePicker } from "./form/FilePicker";
import { fileUrl, formatFileSize, uploadFile } from "../lib/files";

export interface LinkedFile { fileId: string; originalName: string; sizeBytes: number }

/**
 * DOMAIN §7.7: files on QMS records. Kontrolor/Admin add; only Admin removes the link (logged in the
 * AuditLog); the file itself stays stored.
 */
export function EvidenceFiles({ title, files, canAdd, canRemove, add, remove, emptyText = "Nema priloženih fajlova." }: {
  title: string;
  files: LinkedFile[];
  canAdd: boolean;
  canRemove: boolean;
  add: UseMutationResult<void, Error, string>;
  remove: UseMutationResult<void, Error, string>;
  emptyText?: string;
}) {
  const [file, setFile] = useState<File | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<unknown>(null);
  const [removing, setRemoving] = useState<LinkedFile | null>(null);

  const upload = async () => {
    if (!file) return;
    setBusy(true);
    setError(null);
    try {
      const uploaded = await uploadFile(file);
      await add.mutateAsync(uploaded.id);
      setFile(null);
    } catch (e) {
      setError(e);
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="space-y-2">
      <h3 className="text-sm font-semibold">{title}</h3>
      {files.length === 0 ? <p className="text-sm text-slate-600">{emptyText}</p> : (
        <ul className="divide-y divide-slate-100 rounded border border-slate-200">
          {files.map((f) => (
            <li key={f.fileId} className="flex flex-wrap items-center gap-3 px-3 py-2 text-sm">
              <a href={fileUrl(f.fileId)} target="_blank" rel="noreferrer" className="break-all text-blue-800 underline">{f.originalName}</a>
              <span className="text-slate-500 tabular-nums">{formatFileSize(f.sizeBytes)}</span>
              {canRemove && (
                <button type="button" onClick={() => setRemoving(f)} className="ml-auto min-h-9 rounded border border-slate-300 px-2 text-xs">Ukloni vezu</button>
              )}
            </li>
          ))}
        </ul>
      )}
      {canAdd && (
        <div className="space-y-2">
          <FilePicker file={file} onChange={setFile} label="Dodaj fajl" />
          {file && (
            <button type="button" onClick={() => void upload()} disabled={busy} className="min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white disabled:opacity-60">
              {busy ? "Spremanje…" : "Priloži"}
            </button>
          )}
        </div>
      )}
      <ErrorBanner error={error ?? remove.error} />
      <ConfirmDialog open={!!removing} title="Ukloniti vezu s fajlom?" confirmText="Ukloni vezu" danger onCancel={() => setRemoving(null)}
        onConfirm={() => { if (removing) remove.mutate(removing.fileId); setRemoving(null); }}>
        <p className="text-sm">Fajl <strong>{removing?.originalName}</strong> ostaje sačuvan; uklanja se samo veza s ovim zapisom. Promjena se bilježi u istoriji.</p>
      </ConfirmDialog>
    </section>
  );
}
