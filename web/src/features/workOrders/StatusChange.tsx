import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../lib/api";
import { labelOf, workOrderStatus, type WorkOrderStatus } from "../../lib/labels";
import { ErrorBanner } from "../../components/ErrorBanner";
import { inputClass } from "../../components/form/Field";
import { woKey, type WorkOrder } from "./api";
import { useUnsavedChanges } from "../../components/UnsavedChanges";

interface TransitionOption {
  to: WorkOrderStatus;
  allowed: boolean;
  reason: string | null;
  canOverride: boolean;
}

/** UI §6: offers only table transitions; a failing gate is grey with its reason. Admin may override first-piece gates with a reason. */
export function StatusChange({ wo }: { wo: WorkOrder }) {
  const qc = useQueryClient();
  const [open, setOpen] = useState(false);
  const [reasonFor, setReasonFor] = useState<WorkOrderStatus | null>(null);
  const [reason, setReason] = useState("");
  useUnsavedChanges(reason.trim() !== "");
  const options = useQuery({
    queryKey: [...woKey, wo.id, "transitions", wo.version],
    queryFn: () => api.get<TransitionOption[]>(`/api/work-orders/${wo.id}/transitions`),
    enabled: open,
  });
  const change = useMutation({
    mutationFn: (body: { to: WorkOrderStatus; reason: string | null }) =>
      api.post<WorkOrder>(`/api/work-orders/${wo.id}/status`, { ...body, version: wo.version }),
    onSuccess: () => {
      setOpen(false);
      setReasonFor(null);
      setReason("");
      void qc.invalidateQueries({ queryKey: woKey });
    },
  });

  if (!open)
    return (
      <button type="button" onClick={() => setOpen(true)} className="min-h-10 rounded bg-blue-700 px-4 text-sm font-medium text-white">
        Promijeni status
      </button>
    );

  const needsReason = (o: TransitionOption) => (!o.allowed && o.canOverride) || wo.status === "Otkazano";

  return (
    <div className="w-full space-y-2 rounded border border-slate-200 bg-slate-50 p-3">
      <div className="flex items-center justify-between">
        <h2 className="text-sm font-semibold">Novi status</h2>
        <button type="button" onClick={() => setOpen(false)} className="min-h-9 rounded border border-slate-300 bg-white px-3 text-sm">Zatvori</button>
      </div>
      {options.isPending && <p className="text-sm text-slate-600">Učitavanje…</p>}
      {options.data?.length === 0 && <p className="text-sm text-slate-600">Iz statusa {labelOf(workOrderStatus, wo.status).text} nema dozvoljenih prelaza.</p>}
      <ul className="space-y-2">
        {options.data?.map((o) => (
          <li key={o.to} className="space-y-1">
            <div className="flex flex-wrap items-center gap-2">
              <button
                type="button"
                disabled={(!o.allowed && !o.canOverride) || change.isPending}
                onClick={() => (needsReason(o) ? setReasonFor(o.to) : change.mutate({ to: o.to, reason: null }))}
                className={`min-h-11 rounded border px-4 text-sm ${o.allowed ? "border-blue-700 bg-white font-medium text-blue-800" : "border-slate-300 bg-slate-100 text-slate-500"}`}
              >
                {labelOf(workOrderStatus, o.to).text}
              </button>
              {!o.allowed && <span className="text-sm text-slate-600">{o.reason}{o.canOverride ? " Admin može preskočiti uslov uz razlog." : ""}</span>}
            </div>
            {reasonFor === o.to && (
              <div className="flex flex-wrap items-center gap-2 pl-1">
                <label htmlFor={`reason-${o.to}`} className="sr-only">Razlog</label>
                <input id={`reason-${o.to}`} className={`${inputClass} max-w-md`} placeholder={wo.status === "Otkazano" ? "Razlog ponovnog otvaranja" : "Razlog preskakanja uslova"}
                  value={reason} maxLength={300} onChange={(e) => setReason(e.target.value)} />
                <button type="button" disabled={!reason.trim() || change.isPending} onClick={() => change.mutate({ to: o.to, reason: reason.trim() })}
                  className="min-h-11 rounded bg-amber-700 px-4 text-sm font-medium text-white disabled:opacity-50">
                  {wo.status === "Otkazano" ? "Ponovo otvori" : "Preskoči uslov i promijeni"}
                </button>
              </div>
            )}
          </li>
        ))}
      </ul>
      <ErrorBanner error={change.error ?? options.error} onRefresh={() => void qc.invalidateQueries({ queryKey: woKey })} />
    </div>
  );
}
