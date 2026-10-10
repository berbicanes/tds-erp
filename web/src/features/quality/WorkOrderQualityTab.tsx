import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Badge } from "../../components/Badge";
import { ErrorBanner } from "../../components/ErrorBanner";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { Field, inputClass } from "../../components/form/Field";
import { Combobox } from "../../components/form/Combobox";
import { ApiError } from "../../lib/api";
import { hasRole, useMe } from "../../lib/auth";
import { todaySarajevo } from "../../lib/date";
import { EMPTY, formatDate } from "../../lib/format";
import { inspectionPhase, inspectionResult, labelOf } from "../../lib/labels";
import { dateInput, intInput, optionalText, requiredText } from "../../lib/schema";
import { useEmployeeOptions } from "../settings/api";
import type { WorkOrder } from "../workOrders/api";
import { useCreateCheck, useVoidCheck, useWorkOrderChecks, type QualityCheck } from "./api";
import { useUnsavedChanges } from "../../components/UnsavedChanges";

const PHASES = ["LaserskoRezanje", "HidraulicnoPresanje", "ZavrsnaKontrola"] as const;

// Mirrors CreateQualityCheckValidator (Phase 1: checks on work orders only).
const schema = z.object({
  phase: z.enum(PHASES),
  characteristic: requiredText(300, "Unesi karakteristiku / zahtjev crteža."),
  acceptanceCriterion: optionalText(300),
  measuredResult: optionalText(300),
  sampleSize: intInput({ min: 1, minMessage: "Veličina uzorka mora biti najmanje 1." }),
  result: z.enum(["Ceka", "Prihvaceno", "Neuskladjeno"]),
  inspectedByEmployeeId: z.string().nullable().refine((v) => !!v, "Odaberi ko je kontrolisao."),
  inspectionDate: dateInput("Unesi datum kontrole."),
});
type FormIn = z.input<typeof schema>;
type FormOut = z.output<typeof schema>;

function AddCheckForm({ wo, onDone }: { wo: WorkOrder; onDone: () => void }) {
  const create = useCreateCheck();
  const employees = useEmployeeOptions();
  const { register, handleSubmit, control, setError, formState: { errors, isDirty } } = useForm<FormIn, unknown, FormOut>({
    resolver: zodResolver(schema),
    defaultValues: {
      phase: "LaserskoRezanje", characteristic: "", acceptanceCriterion: "", measuredResult: "", sampleSize: "",
      result: "Prihvaceno", inspectedByEmployeeId: null, inspectionDate: todaySarajevo(),
    },
  });
  useUnsavedChanges(isDirty && !create.isSuccess);
  const onSubmit = handleSubmit((v) =>
    create.mutate({ ...v, workOrderId: wo.id, inspectedByEmployeeId: v.inspectedByEmployeeId!, inspectionDate: v.inspectionDate! }, {
      onSuccess: onDone,
      onError: (e) => {
        if (e instanceof ApiError) for (const [f, m] of Object.entries(e.fieldErrors)) setError(f as keyof FormIn, { message: m[0] ?? "" });
      },
    }),
  );
  return (
    <form onSubmit={onSubmit} noValidate className="space-y-3 rounded border border-slate-200 p-3">
      <h3 className="text-sm font-semibold">Nova kontrola</h3>
      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="Faza" htmlFor="qc-phase" required>
          <select id="qc-phase" className={inputClass} {...register("phase")}>
            {PHASES.map((p) => <option key={p} value={p}>{inspectionPhase[p].text}</option>)}
          </select>
        </Field>
        <Field label="Rezultat" htmlFor="qc-result" required>
          <select id="qc-result" className={inputClass} {...register("result")}>
            {Object.entries(inspectionResult).map(([v, l]) => <option key={v} value={v}>{l.text}</option>)}
          </select>
        </Field>
        <Field label="Datum kontrole" htmlFor="qc-date" required error={errors.inspectionDate?.message}>
          <input id="qc-date" type="date" max={todaySarajevo()} className={inputClass} {...register("inspectionDate")} />
        </Field>
      </div>
      <Field label="Karakteristika / zahtjev crteža" htmlFor="qc-char" required error={errors.characteristic?.message}>
        <input id="qc-char" className={inputClass} aria-invalid={!!errors.characteristic} {...register("characteristic")} />
      </Field>
      <div className="grid gap-3 sm:grid-cols-3">
        <Field label="Kriterij prihvatanja" htmlFor="qc-crit" error={errors.acceptanceCriterion?.message}>
          <input id="qc-crit" className={inputClass} {...register("acceptanceCriterion")} />
        </Field>
        <Field label="Izmjereni rezultat" htmlFor="qc-meas" error={errors.measuredResult?.message} hint='npr. "48,95 mm" ili "OK vizuelno"'>
          <input id="qc-meas" className={inputClass} {...register("measuredResult")} />
        </Field>
        <Field label="Veličina uzorka" htmlFor="qc-sample" error={errors.sampleSize?.message}>
          <input id="qc-sample" inputMode="numeric" className={inputClass} {...register("sampleSize")} />
        </Field>
      </div>
      <Field label="Kontrolisao" htmlFor="qc-inspector" required error={errors.inspectedByEmployeeId?.message}>
        <Controller control={control} name="inspectedByEmployeeId" render={({ field }) => (
          <Combobox id="qc-inspector" value={field.value} onChange={field.onChange} invalid={!!errors.inspectedByEmployeeId}
            options={(employees.data ?? []).map((e) => ({ value: e.id, label: e.fullName }))} />
        )} />
      </Field>
      <p className="text-xs text-slate-500">Kontrola se ne uređuje. Pogrešna kontrola se poništi (Admin) i unese nova.</p>
      <ErrorBanner error={create.error instanceof ApiError && Object.keys(create.error.fieldErrors).length ? null : create.error} />
      <div className="flex gap-2">
        <button type="submit" disabled={create.isPending} className="min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white disabled:opacity-60">Spremi kontrolu</button>
        <button type="button" onClick={onDone} className="min-h-11 rounded border border-slate-300 px-4 text-sm">Odustani</button>
      </div>
    </form>
  );
}

function VoidCheck({ check }: { check: QualityCheck }) {
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
      </ConfirmDialog>
      {voidCheck.error && <ErrorBanner error={voidCheck.error} />}
    </>
  );
}

/** Phase 1: simple list + add on the order (ROADMAP). Full quality screens come in Phase 3. */
export function WorkOrderQualityTab({ wo }: { wo: WorkOrder }) {
  const checks = useWorkOrderChecks(wo.id);
  const me = useMe();
  const [adding, setAdding] = useState(false);
  const canCreate = hasRole(me.data, "Admin", "Kontrolor");
  const isAdmin = hasRole(me.data, "Admin");

  return (
    <div className="space-y-3 pt-3">
      {canCreate && !adding && (
        <button type="button" onClick={() => setAdding(true)} className="min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white">Dodaj kontrolu</button>
      )}
      {adding && <AddCheckForm wo={wo} onDone={() => setAdding(false)} />}
      <ErrorBanner error={checks.error} />
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs text-slate-600">
            <tr>
              <th className="px-2 py-2 font-medium">Broj</th>
              <th className="px-2 py-2 font-medium">Faza</th>
              <th className="px-2 py-2 font-medium">Karakteristika</th>
              <th className="px-2 py-2 font-medium">Izmjereno</th>
              <th className="px-2 py-2 font-medium">Rezultat</th>
              <th className="px-2 py-2 font-medium">Kontrolisao</th>
              <th className="px-2 py-2 font-medium">Datum</th>
              {isAdmin && <th className="px-2 py-2"><span className="sr-only">Akcije</span></th>}
            </tr>
          </thead>
          <tbody>
            {checks.data?.length === 0 && <tr><td colSpan={8} className="px-2 py-4 text-center text-slate-600">Nema kontrola na ovom nalogu.</td></tr>}
            {checks.data?.map((c) => (
              <tr key={c.id} className={`border-t border-slate-100 ${c.isVoided ? "text-slate-400" : ""}`}>
                <td className="px-2 py-1.5 tabular-nums">{c.number}{c.isVoided && <span className="block text-xs">Poništena: {c.voidedReason}</span>}</td>
                <td className="px-2 py-1.5">{labelOf(inspectionPhase, c.phase).text}</td>
                <td className="px-2 py-1.5">{c.characteristic}{c.acceptanceCriterion ? <span className="block text-xs text-slate-500">{c.acceptanceCriterion}</span> : null}</td>
                <td className="px-2 py-1.5">{c.measuredResult ?? EMPTY}</td>
                <td className="px-2 py-1.5">{c.isVoided ? "Poništena" : <Badge label={labelOf(inspectionResult, c.result)} />}</td>
                <td className="px-2 py-1.5">{c.inspectedByName}</td>
                <td className="px-2 py-1.5">{formatDate(c.inspectionDate)}</td>
                {isAdmin && <td className="px-2 py-1.5 text-right">{!c.isVoided && <VoidCheck check={c} />}</td>}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
