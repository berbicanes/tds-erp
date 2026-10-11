import { useState } from "react";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { ErrorBanner } from "../../components/ErrorBanner";
import { inputClass } from "../../components/form/Field";
import { useVoidNc, type NonConformity } from "./api";

export function VoidNcButton({ nc }: { nc: NonConformity }) {
  const voidNc = useVoidNc();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  return (
    <>
      <button type="button" onClick={() => setOpen(true)} className="min-h-10 rounded border border-slate-300 px-3 text-sm">Poništi</button>
      <ConfirmDialog open={open} title={`Poništiti ${nc.number}?`} confirmText="Poništi" danger onCancel={() => setOpen(false)}
        onConfirm={() => { if (reason.trim()) { setOpen(false); voidNc.mutate({ id: nc.id, reason: reason.trim(), version: nc.version }); } }}>
        <label htmlFor="nc-void-reason" className="block text-sm font-medium">Razlog *</label>
        <input id="nc-void-reason" className={inputClass} value={reason} maxLength={300} onChange={(e) => setReason(e.target.value)} />
      </ConfirmDialog>
      <ErrorBanner error={voidNc.error} />
    </>
  );
}
