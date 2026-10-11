import { useState } from "react";
import { Badge } from "../../components/Badge";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { ErrorBanner } from "../../components/ErrorBanner";
import { Field, inputClass, textareaClass } from "../../components/form/Field";
import { FilePicker } from "../../components/form/FilePicker";
import { useUnsavedChanges } from "../../components/UnsavedChanges";
import { hasRole, useMe } from "../../lib/auth";
import { todaySarajevo } from "../../lib/date";
import { fileUrl, uploadFile } from "../../lib/files";
import { EMPTY, formatDate } from "../../lib/format";
import { calibrationResult, calibrationStatusAtCheck, labelOf, userLabel, type CalibrationResult } from "../../lib/labels";
import { useCalibrations, useRecordCalibration, useVoidCalibration, type Equipment } from "./api";

/** Same rule as EquipmentCalibration.Record on the server: date + interval months (end of month clamped). */
export function proposeNext(date: string, months: number | null): string {
  if (!date || !months) return "";
  const [y, m, d] = date.split("-").map(Number) as [number, number, number];
  const target = new Date(Date.UTC(y, m - 1 + months, 1));
  const last = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate();
  target.setUTCDate(Math.min(d, last));
  return target.toISOString().slice(0, 10);
}

function RecordForm({ equipment, onDone }: { equipment: Equipment; onDone: () => void }) {
  const record = useRecordCalibration(equipment.id);
  const [date, setDate] = useState(todaySarajevo());
  const [next, setNext] = useState(proposeNext(todaySarajevo(), equipment.calibrationIntervalMonths));
  const [nextTouched, setNextTouched] = useState(false);
  const [performedBy, setPerformedBy] = useState("");
  const [result, setResult] = useState<CalibrationResult>("Uskladjeno");
  const [note, setNote] = useState("");
  const [file, setFile] = useState<File | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const markSaved = useUnsavedChanges(performedBy.trim() !== "" || note.trim() !== "" || file !== null);

  const submit = async () => {
    if (!date) return setError("Unesi datum kalibracije.");
    if (!performedBy.trim()) return setError("Upiši ko je izvršio kalibraciju.");
    if (next && next < date) return setError("Sljedeća kalibracija ne može biti prije datuma kalibracije.");
    setError(null);
    setBusy(true);
    try {
      const certificateFileId = file ? (await uploadFile(file)).id : null;
      await record.mutateAsync({ calibrationDate: date, nextCalibrationDate: next || null, performedBy: performedBy.trim(), result, certificateFileId, note: note.trim() || null });
      markSaved();
      onDone();
    } catch (e) {
      setError(e instanceof Error ? e.message : "Kalibracija nije spremljena.");
    } finally {
      setBusy(false);
    }
  };

  return (
    <div className="space-y-3 rounded border border-slate-200 p-3">
      <h3 className="text-sm font-semibold">Zabilježi kalibraciju</h3>
      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="Datum kalibracije" htmlFor="cal-date" required>
          <input id="cal-date" type="date" max={todaySarajevo()} className={inputClass} value={date}
            onChange={(e) => { setDate(e.target.value); if (!nextTouched) setNext(proposeNext(e.target.value, equipment.calibrationIntervalMonths)); }} />
        </Field>
        <Field label="Sljedeća kalibracija" htmlFor="cal-next"
          hint={equipment.calibrationIntervalMonths ? `Predloženo iz intervala od ${equipment.calibrationIntervalMonths} mj.` : "Prazno = bez datuma kalibracije (upozorenje)."}>
          <input id="cal-next" type="date" className={inputClass} value={next} onChange={(e) => { setNext(e.target.value); setNextTouched(true); }} />
        </Field>
        <Field label="Rezultat" htmlFor="cal-result" required>
          <select id="cal-result" className={inputClass} value={result} onChange={(e) => setResult(e.target.value as CalibrationResult)}>
            {Object.entries(calibrationResult).map(([v, l]) => <option key={v} value={v}>{l.text}</option>)}
          </select>
        </Field>
      </div>
      <Field label="Izvršio (laboratorija / osoba)" htmlFor="cal-by" required>
        <input id="cal-by" className={inputClass} value={performedBy} maxLength={150} onChange={(e) => setPerformedBy(e.target.value)} />
      </Field>
      <Field label="Certifikat o kalibraciji" htmlFor="cal-file">
        <FilePicker file={file} onChange={setFile} label="Odaberi certifikat (PDF ili slika)" />
      </Field>
      <Field label="Napomena" htmlFor="cal-note">
        <textarea id="cal-note" className={textareaClass} value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} />
      </Field>
      {result === "Neuskladjeno" && <p className="rounded bg-red-50 p-2 text-sm text-red-800">Rezultat "Neusklađeno" čini mjerno sredstvo van kalibracije bez obzira na datum.</p>}
      {error && <ErrorBanner error={new Error(error)} />}
      <div className="flex gap-2">
        <button type="button" onClick={() => void submit()} disabled={busy} className="min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white disabled:opacity-60">
          {busy ? "Spremanje…" : "Spremi kalibraciju"}
        </button>
        <button type="button" onClick={onDone} className="min-h-11 rounded border border-slate-300 px-4 text-sm">Odustani</button>
      </div>
    </div>
  );
}

/** DATA_MODEL §9a: "pokaži istoriju kalibracije za MS-003" — one readable list. */
export function CalibrationSection({ equipment, canEdit }: { equipment: Equipment; canEdit: boolean }) {
  const list = useCalibrations(equipment.id);
  const me = useMe();
  const voidCal = useVoidCalibration();
  const [adding, setAdding] = useState(false);
  const [voiding, setVoiding] = useState<{ id: string; version: number } | null>(null);
  const [reason, setReason] = useState("");

  return (
    <section className="max-w-4xl space-y-3 px-4 pb-6">
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="font-semibold">Istorija kalibracija</h2>
        {equipment.calibrationStatus && <Badge label={labelOf(calibrationStatusAtCheck, equipment.calibrationStatus)} />}
        {canEdit && !adding && (
          <button type="button" onClick={() => setAdding(true)} className="ml-auto min-h-10 rounded bg-blue-700 px-4 text-sm font-medium text-white">Zabilježi kalibraciju</button>
        )}
      </div>
      {adding && <RecordForm equipment={equipment} onDone={() => setAdding(false)} />}
      <ErrorBanner error={list.error ?? voidCal.error} />
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="w-full text-sm cards-sm">
          <thead className="bg-slate-50 text-left text-xs text-slate-600">
            <tr>
              <th className="px-2 py-2 font-medium">Datum</th>
              <th className="px-2 py-2 font-medium">Sljedeća</th>
              <th className="px-2 py-2 font-medium">Rezultat</th>
              <th className="px-2 py-2 font-medium">Izvršio</th>
              <th className="px-2 py-2 font-medium">Certifikat</th>
              <th className="px-2 py-2 font-medium">Napomena</th>
              <th className="px-2 py-2 font-medium">Unio</th>
              {hasRole(me.data, "Admin") && <th className="px-2 py-2"><span className="sr-only">Akcije</span></th>}
            </tr>
          </thead>
          <tbody>
            {list.data?.length === 0 && <tr><td colSpan={8} className="px-2 py-4 text-center text-slate-600">Kalibracija još nije zabilježena.</td></tr>}
            {list.data?.map((c) => (
              <tr key={c.id} className={`border-t border-slate-100 ${c.isVoided ? "text-slate-400" : ""}`}>
                <td data-label="Datum" className="px-2 py-1.5">{formatDate(c.calibrationDate)}{c.isVoided && <span className="block text-xs">Poništeno: {c.voidedReason}</span>}</td>
                <td data-label="Sljedeća" className="px-2 py-1.5">{formatDate(c.nextCalibrationDate)}</td>
                <td data-label="Rezultat" className="px-2 py-1.5"><Badge label={labelOf(calibrationResult, c.result)} /></td>
                <td data-label="Izvršio" className="px-2 py-1.5">{c.performedBy}</td>
                <td data-label="Certifikat" className="px-2 py-1.5">{c.certificateFileId ? <a href={fileUrl(c.certificateFileId)} target="_blank" rel="noreferrer" className="text-blue-800 underline">{c.certificateFileName}</a> : EMPTY}</td>
                <td data-label="Napomena" className="px-2 py-1.5">{c.note ?? EMPTY}</td>
                <td data-label="Unio" className="px-2 py-1.5">{userLabel(c.createdByName)}</td>
                {hasRole(me.data, "Admin") && (
                  <td className="px-2 py-1.5 text-right">
                    {!c.isVoided && <button type="button" onClick={() => setVoiding({ id: c.id, version: c.version })} className="min-h-9 rounded border border-slate-300 px-2 text-xs">Poništi</button>}
                  </td>
                )}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
      <ConfirmDialog open={!!voiding} title="Poništiti kalibraciju?" confirmText="Poništi" danger onCancel={() => setVoiding(null)}
        onConfirm={() => { if (voiding && reason.trim()) { voidCal.mutate({ ...voiding, reason: reason.trim() }); setVoiding(null); setReason(""); } }}>
        <label htmlFor="cal-void-reason" className="block text-sm font-medium">Razlog *</label>
        <input id="cal-void-reason" className={inputClass} value={reason} maxLength={300} onChange={(e) => setReason(e.target.value)} />
      </ConfirmDialog>
    </section>
  );
}
