import { useState } from "react";
import { Link } from "react-router";
import { Badge } from "../../components/Badge";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { ErrorBanner } from "../../components/ErrorBanner";
import { inputClass } from "../../components/form/Field";
import { hasRole, useMe } from "../../lib/auth";
import { EMPTY, formatDate } from "../../lib/format";
import { calibrationStatusAtCheck, inspectionPhase, inspectionResult, labelOf, type CalibrationStatusAtCheck } from "../../lib/labels";
import { useVoidCheck, type QualityCheck } from "./api";

export const needsCalibrationWarning = (s: CalibrationStatusAtCheck) => s === "VanKalibracije" || s === "BezDatumaKalibracije";

export function VoidCheckButton({ check }: { check: QualityCheck }) {
  const voidCheck = useVoidCheck();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  return (
    <>
      <button type="button" onClick={() => setOpen(true)} className="min-h-9 rounded border border-slate-300 px-2 text-xs">Poništi</button>
      <ConfirmDialog open={open} title={`Poništiti kontrolu ${check.number}?`} confirmText="Poništi" danger onCancel={() => setOpen(false)}
        onConfirm={() => { if (reason.trim()) { setOpen(false); voidCheck.mutate({ id: check.id, reason: reason.trim(), version: check.version }); } }}>
        <label htmlFor={`void-${check.id}`} className="block text-sm font-medium">Razlog *</label>
        <input id={`void-${check.id}`} className={inputClass} value={reason} maxLength={300} onChange={(e) => setReason(e.target.value)} />
        <p className="mt-2 text-xs text-slate-500">Pogrešna kontrola se poništava i unosi nova; ništa se ne briše.</p>
      </ConfirmDialog>
      {voidCheck.error && <ErrorBanner error={voidCheck.error} />}
    </>
  );
}

/** NC column: the existing NC, or the offer for a failed check (DOMAIN §7.1a). */
export function NcCell({ check }: { check: QualityCheck }) {
  const me = useMe();
  if (check.nonConformityId) return <Link to={`/kvalitet/nc/${check.nonConformityId}`} className="text-blue-800 underline">{check.nonConformityNumber}</Link>;
  if (check.result === "Neuskladjeno" && !check.isVoided && hasRole(me.data, "Admin", "Kontrolor"))
    return <Link to={`/kvalitet/nc/nova?kontrola=${check.id}`} className="font-medium text-red-800 underline">Otvori NC</Link>;
  return <>{EMPTY}</>;
}

export function CalibrationMark({ status }: { status: CalibrationStatusAtCheck }) {
  return needsCalibrationWarning(status) ? <Badge label={labelOf(calibrationStatusAtCheck, status)} /> : null;
}

/** Checks of one order or one lot (UI §6 Kvalitet: by phase). */
export function CheckTable({ checks, emptyText }: { checks: QualityCheck[] | undefined; emptyText: string }) {
  const me = useMe();
  const isAdmin = hasRole(me.data, "Admin");
  return (
    <div className="overflow-x-auto rounded border border-slate-200">
      <table className="w-full text-sm cards-sm">
        <thead className="bg-slate-50 text-left text-xs text-slate-600">
          <tr>
            <th className="px-2 py-2 font-medium">Broj</th>
            <th className="px-2 py-2 font-medium">Faza</th>
            <th className="px-2 py-2 font-medium">Karakteristika</th>
            <th className="px-2 py-2 font-medium">Izmjereno</th>
            <th className="px-2 py-2 font-medium">Rezultat</th>
            <th className="px-2 py-2 font-medium">Mjerno sredstvo</th>
            <th className="px-2 py-2 font-medium">NC</th>
            <th className="px-2 py-2 font-medium">Kontrolisao</th>
            <th className="px-2 py-2 font-medium">Datum</th>
            {isAdmin && <th className="px-2 py-2"><span className="sr-only">Akcije</span></th>}
          </tr>
        </thead>
        <tbody>
          {checks?.length === 0 && <tr><td colSpan={10} className="px-2 py-4 text-center text-slate-600">{emptyText}</td></tr>}
          {checks?.map((c) => (
            <tr key={c.id} className={`border-t border-slate-100 ${c.isVoided ? "text-slate-400" : ""}`}>
              <td data-label="Broj" className="px-2 py-1.5 tabular-nums">
                <Link to={`/kvalitet/kontrole/${c.id}`} className="text-blue-800 underline">{c.number}</Link>
                {c.evidenceCount > 0 && <span className="block text-xs text-slate-500">Dokazi: {c.evidenceCount}</span>}
                {c.isVoided && <span className="block text-xs">Poništena: {c.voidedReason}</span>}
              </td>
              <td data-label="Faza" className="px-2 py-1.5">{labelOf(inspectionPhase, c.phase).text}</td>
              <td data-label="Karakteristika" className="px-2 py-1.5">{c.characteristic}{c.acceptanceCriterion ? <span className="block text-xs text-slate-500">{c.acceptanceCriterion}</span> : null}</td>
              <td data-label="Izmjereno" className="px-2 py-1.5">{c.measuredResult ?? EMPTY}</td>
              <td data-label="Rezultat" className="px-2 py-1.5">{c.isVoided ? "Poništena" : <Badge label={labelOf(inspectionResult, c.result)} />}</td>
              <td data-label="Mjerno sredstvo" className="px-2 py-1.5">{c.measuringEquipmentCode ?? EMPTY} <CalibrationMark status={c.calibrationStatusAtCheck} /></td>
              <td data-label="NC" className="px-2 py-1.5"><NcCell check={c} /></td>
              <td data-label="Kontrolisao" className="px-2 py-1.5">{c.inspectedByName}</td>
              <td data-label="Datum" className="px-2 py-1.5">{formatDate(c.inspectionDate)}</td>
              {isAdmin && <td className="px-2 py-1.5 text-right">{!c.isVoided && <VoidCheckButton check={c} />}</td>}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
