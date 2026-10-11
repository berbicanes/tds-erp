import { useState } from "react";
import { Badge } from "../../components/Badge";
import { ErrorBanner, WarningList } from "../../components/ErrorBanner";
import { Combobox } from "../../components/form/Combobox";
import { Field, inputClass } from "../../components/form/Field";
import { hasRole, useMe } from "../../lib/auth";
import { formatQuantity, parseDecimal } from "../../lib/format";
import { labelOf, receiptDecision } from "../../lib/labels";
import { itemTitle, qtyWithUnit } from "../../lib/material";
import { useLots } from "../stock/lotsApi";
import type { WorkOrder } from "./api";
import { useLinkLot, useSetReservation, useWorkOrderMaterials, type WorkOrderMaterial } from "./materialsApi";
import { useUnsavedChanges } from "../../components/UnsavedChanges";

const reservable = { page: 1, sort: "lotNumber", q: "", filters: { receiptDecision: ["NaKontroli", "Prihvacen"] } };

function LinkLotForm({ wo, onWarnings }: { wo: WorkOrder; onWarnings: (w: string[]) => void }) {
  const lots = useLots(reservable, "&pageSize=200");
  const link = useLinkLot(wo.id);
  const [lotId, setLotId] = useState<string | null>(null);
  const [qty, setQty] = useState("");
  const [error, setError] = useState<string | null>(null);
  useUnsavedChanges(lotId !== null || qty.trim() !== "");
  const selected = lots.data?.items.find((l) => l.id === lotId);

  const submit = () => {
    if (!lotId) return setError("Odaberi lot.");
    const n = qty.trim() === "" ? 0 : parseDecimal(qty);
    if (n === null || n < 0) return setError("Rezervacija mora biti 0 ili veća (0 = samo veza za sljedivost).");
    setError(null);
    link.mutate({ materialLotId: lotId, reservedQty: n }, { onSuccess: (r) => { onWarnings(r.warnings); setLotId(null); setQty(""); } });
  };

  return (
    <div className="space-y-3 rounded border border-slate-200 p-3">
      <h3 className="text-sm font-semibold">Veži lot i rezerviši</h3>
      <div className="grid gap-3 sm:grid-cols-3">
        <div className="sm:col-span-2">
          <Field label="Lot (na kontroli ili prihvaćen)" htmlFor="link-lot" required>
            <Combobox id="link-lot" value={lotId} onChange={setLotId}
              options={(lots.data?.items ?? []).map((l) => ({
                value: l.id,
                label: l.lotNumber,
                detail: `${itemTitle({ name: l.itemName, grade: l.itemGrade, thicknessMm: l.itemThicknessMm })} · na stanju ${qtyWithUnit(l.onHand, l.unit)}`,
              }))} />
          </Field>
        </div>
        <Field label={`Rezervacija${selected ? ` (${selected.unit})` : ""}`} htmlFor="link-qty" hint="0 = samo veza (sljedivost)">
          <input id="link-qty" inputMode="decimal" className={inputClass} value={qty} onChange={(e) => setQty(e.target.value)} />
        </Field>
      </div>
      {error && <p className="text-sm text-red-700">{error}</p>}
      <ErrorBanner error={link.error} />
      <button type="button" onClick={submit} disabled={link.isPending} className="min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white disabled:opacity-60">Veži lot</button>
    </div>
  );
}

function ReservationCell({ wo, m, canEdit, onWarnings }: { wo: WorkOrder; m: WorkOrderMaterial; canEdit: boolean; onWarnings: (w: string[]) => void }) {
  const set = useSetReservation(wo.id);
  const [editing, setEditing] = useState(false);
  const [value, setValue] = useState(String(m.reservedQty).replace(".", ","));
  // Version when editing started (a refetch must not move it, see lib/formSync.ts).
  const [baseVersion, setBaseVersion] = useState(m.version);
  useUnsavedChanges(editing && value !== String(m.reservedQty).replace(".", ","));
  if (!editing)
    return (
      <span className="inline-flex items-center gap-2">
        {qtyWithUnit(m.reservedQty, m.unit)}
        {canEdit && (
          <button type="button" onClick={() => { setBaseVersion(m.version); setValue(String(m.reservedQty).replace(".", ",")); setEditing(true); }}
            className="min-h-8 rounded border border-slate-300 px-2 text-xs">Promijeni</button>
        )}
      </span>
    );
  const n = parseDecimal(value);
  return (
    <span className="inline-flex flex-wrap items-center gap-2">
      <input aria-label={`Rezervacija za lot ${m.lotNumber}`} inputMode="decimal" className="h-9 w-24 rounded border border-slate-300 px-2 text-right" value={value} onChange={(e) => setValue(e.target.value)} />
      <button type="button" disabled={n === null || n < 0 || set.isPending}
        onClick={() => set.mutate({ linkId: m.id, reservedQty: n!, version: baseVersion }, { onSuccess: (r) => { onWarnings(r.warnings); setEditing(false); } })}
        className="min-h-8 rounded bg-blue-700 px-2 text-xs text-white disabled:opacity-50">Spremi</button>
      <button type="button" onClick={() => setEditing(false)} className="min-h-8 rounded border border-slate-300 px-2 text-xs">Odustani</button>
      {set.error && <span className="block w-full"><ErrorBanner error={set.error} /></span>}
    </span>
  );
}

/** UI §6 "Materijal": linked lots, reservations, consumption. Links are never removed (traceability). */
export function WorkOrderMaterialsTab({ wo }: { wo: WorkOrder }) {
  const materials = useWorkOrderMaterials(wo.id);
  const me = useMe();
  const [warnings, setWarnings] = useState<string[]>([]);
  const canEdit = hasRole(me.data, "Admin", "Kontrolor") && !["Isporuceno", "Otkazano"].includes(wo.status);

  return (
    <div className="space-y-3 pt-3">
      <WarningList warnings={warnings} />
      {canEdit && <LinkLotForm wo={wo} onWarnings={setWarnings} />}
      <ErrorBanner error={materials.error} />
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="w-full text-sm cards-sm">
          <thead className="bg-slate-50 text-left text-xs text-slate-600">
            <tr>
              <th className="px-2 py-2 font-medium">Lot</th>
              <th className="px-2 py-2 font-medium">Artikal</th>
              <th className="px-2 py-2 font-medium">Odluka</th>
              <th className="px-2 py-2 font-medium">Rezervisano</th>
              <th className="px-2 py-2 text-right font-medium">Utrošeno</th>
              <th className="px-2 py-2 text-right font-medium">Na stanju lota</th>
              <th className="px-2 py-2 text-right font-medium">Raspoloživo</th>
            </tr>
          </thead>
          <tbody>
            {materials.data?.length === 0 && <tr><td colSpan={7} className="px-2 py-4 text-center text-slate-600">Za nalog još nije vezan nijedan lot.</td></tr>}
            {materials.data?.map((m) => (
              <tr key={m.id} className="border-t border-slate-100">
                <td data-label="Lot" className="px-2 py-1.5 font-medium">{m.lotNumber}</td>
                <td data-label="Artikal" className="px-2 py-1.5">{itemTitle({ name: m.itemName, grade: m.itemGrade, thicknessMm: m.itemThicknessMm })}</td>
                <td data-label="Odluka" className="px-2 py-1.5"><Badge label={labelOf(receiptDecision, m.receiptDecision)} /></td>
                <td data-label="Rezervisano" className="px-2 py-1.5 tabular-nums"><ReservationCell wo={wo} m={m} canEdit={canEdit} onWarnings={setWarnings} /></td>
                <td data-label="Utrošeno" className="px-2 py-1.5 text-right tabular-nums">{formatQuantity(m.consumedQty)} {m.unit}</td>
                <td data-label="Na stanju lota" className="px-2 py-1.5 text-right tabular-nums">{qtyWithUnit(m.lotOnHand, m.unit)}</td>
                <td data-label="Raspoloživo" className={`px-2 py-1.5 text-right tabular-nums ${m.lotAvailable != null && m.lotAvailable < 0 ? "font-semibold text-red-700" : ""}`}>{qtyWithUnit(m.lotAvailable, m.unit)}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
