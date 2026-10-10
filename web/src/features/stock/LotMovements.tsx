import { useState } from "react";
import { useMutation, useQuery, useQueryClient } from "@tanstack/react-query";
import { api } from "../../lib/api";
import { hasRole, useMe } from "../../lib/auth";
import { formatDate, formatQuantity } from "../../lib/format";
import { labelOf, stockMovementType, userLabel, type StockMovementType } from "../../lib/labels";
import { parseDecimal } from "../../lib/format";
import { todaySarajevo } from "../../lib/date";
import { Badge } from "../../components/Badge";
import { ErrorBanner } from "../../components/ErrorBanner";
import { Field, inputClass } from "../../components/form/Field";
import { itemKey } from "./itemsApi";
import { lotKey, type Lot } from "./lotsApi";

interface Movement {
  id: string;
  type: StockMovementType;
  quantity: number;
  date: string;
  reason: string | null;
  productionEntryId: string | null;
  createdByName: string | null;
  isVoided: boolean;
  voidedReason: string | null;
}

function ManualMovementForm({ lot }: { lot: Lot }) {
  const qc = useQueryClient();
  const [type, setType] = useState<"Korekcija" | "Otpis">("Korekcija");
  const [qty, setQty] = useState("");
  const [date, setDate] = useState(todaySarajevo());
  const [reason, setReason] = useState("");
  const [error, setError] = useState<string | null>(null);
  const save = useMutation({
    mutationFn: (body: { type: string; quantity: number; date: string; reason: string }) => api.post(`/api/lots/${lot.id}/movements`, body),
    onSuccess: () => {
      setQty("");
      setReason("");
      void qc.invalidateQueries({ queryKey: lotKey });
      void qc.invalidateQueries({ queryKey: itemKey });
      void qc.invalidateQueries({ queryKey: ["stock"] });
    },
  });

  const submit = () => {
    const n = parseDecimal(qty);
    if (n === null || n === 0) return setError(type === "Otpis" ? "Unesi količinu otpisa veću od 0." : "Unesi količinu korekcije, npr. -1,5 ili 2.");
    if (type === "Otpis" && n < 0) return setError("Količina otpisa se unosi kao pozitivan broj.");
    if (!reason.trim()) return setError("Razlog je obavezan.");
    setError(null);
    save.mutate({ type, quantity: n, date, reason: reason.trim() });
  };

  return (
    <div className="space-y-3 rounded border border-slate-200 p-3">
      <h3 className="text-sm font-semibold">Korekcija ili otpis</h3>
      <div className="flex gap-2">
        {(["Korekcija", "Otpis"] as const).map((t) => (
          <button key={t} type="button" aria-pressed={type === t} onClick={() => setType(t)}
            className={`min-h-11 rounded border px-3 text-sm ${type === t ? "border-blue-700 bg-blue-50 font-semibold" : "border-slate-300"}`}>
            {t}
          </button>
        ))}
      </div>
      <div className="grid gap-3 sm:grid-cols-3">
        <Field label={`Količina (${lot.unit})`} htmlFor="mv-qty" required hint={type === "Korekcija" ? "+ dodaje, − oduzima" : `Najviše ${formatQuantity(lot.onHand)}`}>
          <input id="mv-qty" inputMode="decimal" className={inputClass} value={qty} onChange={(e) => setQty(e.target.value)} />
        </Field>
        <Field label="Datum" htmlFor="mv-date" required>
          <input id="mv-date" type="date" max={todaySarajevo()} className={inputClass} value={date} onChange={(e) => setDate(e.target.value)} />
        </Field>
        <Field label="Razlog" htmlFor="mv-reason" required>
          <input id="mv-reason" className={inputClass} value={reason} maxLength={300} onChange={(e) => setReason(e.target.value)} />
        </Field>
      </div>
      {error && <p className="text-sm text-red-700">{error}</p>}
      <ErrorBanner error={save.error} />
      <button type="button" onClick={submit} disabled={save.isPending} className="min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white disabled:opacity-60">
        Spremi pokret
      </button>
    </div>
  );
}

export function LotMovements({ lot }: { lot: Lot }) {
  const me = useMe();
  const movements = useQuery({
    queryKey: [...lotKey, lot.id, "movements"],
    queryFn: () => api.get<Movement[]>(`/api/lots/${lot.id}/movements`),
  });

  return (
    <section className="space-y-3">
      <h3 className="font-semibold">Pokreti zalihe</h3>
      {hasRole(me.data, "Admin", "Kontrolor") && !lot.isVoided && <ManualMovementForm lot={lot} />}
      <ErrorBanner error={movements.error} />
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs text-slate-600">
            <tr>
              <th className="px-2 py-2 font-medium">Datum</th>
              <th className="px-2 py-2 font-medium">Vrsta</th>
              <th className="px-2 py-2 text-right font-medium">Količina</th>
              <th className="px-2 py-2 font-medium">Razlog</th>
              <th className="px-2 py-2 font-medium">Unio</th>
            </tr>
          </thead>
          <tbody>
            {movements.data?.length === 0 && <tr><td colSpan={5} className="px-2 py-4 text-center text-slate-600">Nema pokreta.</td></tr>}
            {movements.data?.map((m) => (
              <tr key={m.id} className={`border-t border-slate-100 ${m.isVoided ? "text-slate-400 line-through" : ""}`}>
                <td className="px-2 py-1.5">{formatDate(m.date)}</td>
                <td className="px-2 py-1.5"><Badge label={labelOf(stockMovementType, m.type)} /></td>
                <td className="px-2 py-1.5 text-right tabular-nums">{m.quantity > 0 ? "+" : ""}{formatQuantity(m.quantity)} {lot.unit}</td>
                <td className="px-2 py-1.5">{m.reason ?? (m.productionEntryId ? "Dnevni zapis" : "")}{m.isVoided && m.voidedReason ? ` (poništeno: ${m.voidedReason})` : ""}</td>
                <td className="px-2 py-1.5">{userLabel(m.createdByName)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
