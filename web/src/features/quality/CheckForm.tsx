import { useState } from "react";
import { Link } from "react-router";
import { Controller, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { ErrorBanner, WarningList } from "../../components/ErrorBanner";
import { Field, inputClass } from "../../components/form/Field";
import { Combobox } from "../../components/form/Combobox";
import { FilePicker } from "../../components/form/FilePicker";
import { useUnsavedChanges } from "../../components/UnsavedChanges";
import { ApiError } from "../../lib/api";
import { todaySarajevo } from "../../lib/date";
import { uploadFile } from "../../lib/files";
import { calibrationStatusAtCheck, inspectionPhase, inspectionResult, labelOf, type InspectionPhase } from "../../lib/labels";
import { dateInput, intInput, optionalText, requiredText } from "../../lib/schema";
import { useEmployeeOptions } from "../settings/api";
import { useEquipmentOptions } from "../equipment/api";
import { useCreateCheck, type QualityCheck } from "./api";

const ORDER_PHASES = ["LaserskoRezanje", "HidraulicnoPresanje", "ZavrsnaKontrola"] as const;

// Mirrors CreateQualityCheckValidator.
const schema = z.object({
  phase: z.enum(["UlazniMaterijal", ...ORDER_PHASES]),
  characteristic: requiredText(300, "Unesi karakteristiku / zahtjev crteža."),
  acceptanceCriterion: optionalText(300),
  measuredResult: optionalText(300),
  sampleSize: intInput({ min: 1, minMessage: "Veličina uzorka mora biti najmanje 1." }),
  result: z.enum(["Ceka", "Prihvaceno", "Neuskladjeno"]),
  inspectedByEmployeeId: z.string().nullable().refine((v) => !!v, "Odaberi ko je kontrolisao."),
  inspectionDate: dateInput("Unesi datum kontrole."),
  measuringEquipmentId: z.string().nullable(),
});
type FormIn = z.input<typeof schema>;
type FormOut = z.output<typeof schema>;

export type CheckOwner = { workOrderId: string } | { materialLotId: string };

/** After saving: calibration warnings and, for a failed check, the offer to open an NC (DOMAIN §7.1 — offer only). */
function SavedCheck({ check, warnings, onClose }: { check: QualityCheck; warnings: string[]; onClose: () => void }) {
  return (
    <div className="space-y-2 rounded border border-slate-200 p-3">
      <p className="text-sm">Kontrola <strong>{check.number}</strong> je spremljena.</p>
      <WarningList warnings={warnings} />
      {check.result === "Neuskladjeno" && (
        <div className="space-y-2 rounded bg-red-50 p-3 text-sm text-red-900">
          <p>Kontrola nije prošla. Otvoriti neusaglašenost (NC) sa podacima iz ove kontrole?</p>
          <div className="flex flex-wrap gap-2">
            <Link to={`/kvalitet/nc/nova?kontrola=${check.id}`} className="flex min-h-11 items-center rounded bg-red-700 px-4 font-medium text-white">Otvori NC</Link>
            <button type="button" onClick={onClose} className="min-h-11 rounded border border-red-300 bg-white px-4">Ne sada</button>
          </div>
        </div>
      )}
      {check.result !== "Neuskladjeno" && <button type="button" onClick={onClose} className="min-h-11 rounded border border-slate-300 px-4 text-sm">Zatvori</button>}
    </div>
  );
}

export function CheckForm({ owner, onDone }: { owner: CheckOwner; onDone: () => void }) {
  const create = useCreateCheck();
  const employees = useEmployeeOptions();
  const instruments = useEquipmentOptions("MjernoSredstvo");
  const forLot = "materialLotId" in owner;
  const phases: readonly InspectionPhase[] = forLot ? ["UlazniMaterijal"] : ORDER_PHASES;
  const [file, setFile] = useState<File | null>(null);
  const [saved, setSaved] = useState<{ check: QualityCheck; warnings: string[] } | null>(null);
  const [uploadError, setUploadError] = useState<unknown>(null);

  const { register, handleSubmit, control, setError, formState: { errors, isDirty, isSubmitting } } = useForm<FormIn, unknown, FormOut>({
    resolver: zodResolver(schema),
    defaultValues: {
      phase: forLot ? "UlazniMaterijal" : "LaserskoRezanje", characteristic: "", acceptanceCriterion: "", measuredResult: "", sampleSize: "",
      result: "Prihvaceno", inspectedByEmployeeId: null, inspectionDate: todaySarajevo(), measuringEquipmentId: null,
    },
  });
  const markSaved = useUnsavedChanges((isDirty || file !== null) && !saved);
  const instrumentId = useWatch({ control, name: "measuringEquipmentId" });
  const instrument = instruments.data?.find((i) => i.id === instrumentId);

  const onSubmit = handleSubmit(async (v) => {
    setUploadError(null);
    let evidenceFileIds: string[] = [];
    try {
      if (file) evidenceFileIds = [(await uploadFile(file)).id];
    } catch (e) {
      setUploadError(e);
      return;
    }
    create.mutate({
      ...v, workOrderId: forLot ? null : owner.workOrderId, materialLotId: forLot ? owner.materialLotId : null,
      inspectedByEmployeeId: v.inspectedByEmployeeId!, inspectionDate: v.inspectionDate!, evidenceFileIds,
    }, {
      onSuccess: (r) => { markSaved(); setSaved({ check: r.data, warnings: r.warnings }); },
      onError: (e) => {
        if (e instanceof ApiError) for (const [f, m] of Object.entries(e.fieldErrors)) setError(f as keyof FormIn, { message: m[0] ?? "" });
      },
    });
  });

  if (saved) return <SavedCheck check={saved.check} warnings={saved.warnings} onClose={onDone} />;

  return (
    <form onSubmit={onSubmit} noValidate className="space-y-3 rounded border border-slate-200 p-3">
      <h3 className="text-sm font-semibold">{forLot ? "Nova ulazna kontrola" : "Nova kontrola"}</h3>
      <div className="grid gap-3 sm:grid-cols-3">
        {phases.length > 1 ? (
          <Field label="Faza" htmlFor="qc-phase" required>
            <select id="qc-phase" className={inputClass} {...register("phase")}>
              {phases.map((p) => <option key={p} value={p}>{inspectionPhase[p].text}</option>)}
            </select>
          </Field>
        ) : (
          <div>
            <p className="mb-1 text-sm font-medium">Faza</p>
            <p className="flex h-11 items-center text-sm">{inspectionPhase[phases[0]!].text}</p>
          </div>
        )}
        <Field label="Rezultat" htmlFor="qc-result" required>
          <select id="qc-result" className={inputClass} {...register("result")}>
            {Object.entries(inspectionResult).map(([v, l]) => <option key={v} value={v}>{l.text}</option>)}
          </select>
        </Field>
        <Field label="Datum kontrole" htmlFor="qc-date" required error={errors.inspectionDate?.message}>
          <input id="qc-date" type="date" max={todaySarajevo()} className={inputClass} {...register("inspectionDate")} />
        </Field>
      </div>
      <Field label="Karakteristika / zahtjev crteža" htmlFor="qc-char" required error={errors.characteristic?.message}
        hint={forLot ? 'npr. "Certifikat 3.1 odgovara heat broju" ili "Debljina 3 mm"' : undefined}>
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
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Kontrolisao" htmlFor="qc-inspector" required error={errors.inspectedByEmployeeId?.message}>
          <Controller control={control} name="inspectedByEmployeeId" render={({ field }) => (
            <Combobox id="qc-inspector" value={field.value} onChange={field.onChange} invalid={!!errors.inspectedByEmployeeId}
              options={(employees.data ?? []).map((e) => ({ value: e.id, label: e.fullName }))} />
          )} />
        </Field>
        <Field label="Mjerno sredstvo" htmlFor="qc-instrument" error={errors.measuringEquipmentId?.message}>
          <Controller control={control} name="measuringEquipmentId" render={({ field }) => (
            <Combobox id="qc-instrument" value={field.value} onChange={field.onChange} allowEmpty emptyLabel="Bez mjernog sredstva"
              options={(instruments.data ?? []).map((i) => ({ value: i.id, label: `${i.code} ${i.name}`, detail: labelOf(calibrationStatusAtCheck, i.calibrationStatus).text }))} />
          )} />
        </Field>
      </div>
      {instrument?.calibrationStatus === "VanKalibracije" && (
        <p role="alert" className="rounded bg-red-50 p-2 text-sm text-red-800">Mjerno sredstvo {instrument.code} je van kalibracije. Kontrola se može spremiti, ali dobija trajnu oznaku.</p>
      )}
      {instrument?.calibrationStatus === "BezDatumaKalibracije" && (
        <p role="alert" className="rounded bg-amber-50 p-2 text-sm text-amber-900">Mjerno sredstvo {instrument.code} nema datum kalibracije. Kontrola se može spremiti, ali dobija trajnu oznaku.</p>
      )}
      <Field label="Dokaz / zapis" htmlFor="qc-file">
        <FilePicker file={file} onChange={setFile} label="Priloži mjerni zapis ili fotografiju" />
      </Field>
      <p className="text-xs text-slate-500">Kontrola se ne uređuje. Pogrešna kontrola se poništi (Admin) i unese nova; dokazi se mogu dodati i kasnije.</p>
      <ErrorBanner error={uploadError ?? (create.error instanceof ApiError && Object.keys(create.error.fieldErrors).length ? null : create.error)} />
      <div className="flex gap-2">
        <button type="submit" disabled={create.isPending || isSubmitting} className="min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white disabled:opacity-60">
          {create.isPending || isSubmitting ? "Spremanje…" : "Spremi kontrolu"}
        </button>
        <button type="button" onClick={onDone} className="min-h-11 rounded border border-slate-300 px-4 text-sm">Odustani</button>
      </div>
    </form>
  );
}
