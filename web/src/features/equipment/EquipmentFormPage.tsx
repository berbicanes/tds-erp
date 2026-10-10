import { useEffect } from "react";
import { useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate, useParams } from "react-router";
import { Field, inputClass } from "../../components/form/Field";
import { ErrorBanner } from "../../components/ErrorBanner";
import { PageHeader } from "../../components/PageHeader";
import { ApiError } from "../../lib/api";
import { hasRole, useMe } from "../../lib/auth";
import { equipmentKind } from "../../lib/labels";
import { formatDate } from "../../lib/format";
import { dateInput, intInput, optionalText, requiredText } from "../../lib/schema";
import { useUnsavedChangesPrompt } from "../../hooks/useUnsavedChangesPrompt";
import { useEquipment, useSaveEquipment } from "./api";

// Mirrors SaveEquipmentValidator.
const schema = z
  .object({
    code: requiredText(20, "Unesi oznaku, npr. M-05 ili MS-001."),
    name: requiredText(120, "Unesi naziv."),
    kind: z.enum(["Masina", "MjernoSredstvo"]),
    manufacturer: optionalText(80),
    serialNumber: optionalText(60),
    calibrationIntervalMonths: intInput({ min: 1, minMessage: "Interval kalibracije mora biti između 1 i 120 mjeseci." }),
    lastCalibrationDate: dateInput(),
    nextCalibrationDate: dateInput(),
    isActive: z.boolean(),
  })
  .transform((v) =>
    v.kind === "Masina" ? { ...v, calibrationIntervalMonths: null, lastCalibrationDate: null, nextCalibrationDate: null } : v,
  );
type FormIn = z.input<typeof schema>;
type FormOut = z.output<typeof schema>;

/** Same rule as Equipment.SetCalibration on the server; shown only as a preview. */
function addMonths(date: string, months: number): string {
  const [y, m, d] = date.split("-").map(Number) as [number, number, number];
  const target = new Date(Date.UTC(y, m - 1 + months, 1));
  const lastDay = new Date(Date.UTC(target.getUTCFullYear(), target.getUTCMonth() + 1, 0)).getUTCDate();
  target.setUTCDate(Math.min(d, lastDay));
  return target.toISOString().slice(0, 10);
}

export function EquipmentFormPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const me = useMe();
  const canEdit = hasRole(me.data, "Admin", "Kontrolor");
  const equipment = useEquipment(id);
  const save = useSaveEquipment(id);

  const { register, handleSubmit, reset, control, setError, formState: { errors, isDirty, isSubmitSuccessful } } = useForm<FormIn, unknown, FormOut>({
    resolver: zodResolver(schema),
    defaultValues: { code: "", name: "", kind: "Masina", manufacturer: "", serialNumber: "", calibrationIntervalMonths: "", lastCalibrationDate: "", nextCalibrationDate: "", isActive: true },
  });

  useEffect(() => {
    const e = equipment.data;
    if (e)
      reset({
        code: e.code, name: e.name, kind: e.kind, manufacturer: e.manufacturer ?? "", serialNumber: e.serialNumber ?? "",
        calibrationIntervalMonths: e.calibrationIntervalMonths?.toString() ?? "", lastCalibrationDate: e.lastCalibrationDate ?? "",
        nextCalibrationDate: e.nextCalibrationDate ?? "", isActive: e.isActive,
      });
  }, [equipment.data, reset]);

  const prompt = useUnsavedChangesPrompt(canEdit && isDirty && !isSubmitSuccessful);
  const [kind, interval, last] = useWatch({ control, name: ["kind", "calibrationIntervalMonths", "lastCalibrationDate"] });
  const intervalNumber = Number(interval);
  const derivedNext = kind === "MjernoSredstvo" && Number.isInteger(intervalNumber) && intervalNumber > 0 && last ? addMonths(last, intervalNumber) : null;

  const onSubmit = handleSubmit((values) =>
    save.mutate({ ...values, version: equipment.data?.version ?? 0 }, {
      onSuccess: () => navigate("/oprema"),
      onError: (e) => {
        if (e instanceof ApiError)
          for (const [f, m] of Object.entries(e.fieldErrors)) setError(f as keyof FormIn, { message: m[0] ?? "" });
      },
    }),
  );

  if (id && equipment.isPending) return <p className="p-4 text-slate-600">Učitavanje…</p>;
  if (id && equipment.isError) return <div className="p-4"><ErrorBanner error={equipment.error} /></div>;

  return (
    <>
      <PageHeader title={id ? `Oprema ${equipment.data?.code ?? ""}` : "Nova oprema"} />
      <form onSubmit={onSubmit} noValidate className="max-w-xl space-y-4 px-4 py-4">
        {prompt}
        <fieldset disabled={!canEdit} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Oznaka" htmlFor="code" required error={errors.code?.message}>
              <input id="code" className={inputClass} aria-invalid={!!errors.code} {...register("code")} />
            </Field>
            <Field label="Vrsta" htmlFor="kind" required>
              <select id="kind" className={inputClass} {...register("kind")}>
                {Object.entries(equipmentKind).map(([v, l]) => <option key={v} value={v}>{l.text}</option>)}
              </select>
            </Field>
          </div>
          <Field label="Naziv" htmlFor="name" required error={errors.name?.message}>
            <input id="name" className={inputClass} aria-invalid={!!errors.name} {...register("name")} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Proizvođač" htmlFor="manufacturer" error={errors.manufacturer?.message}>
              <input id="manufacturer" className={inputClass} {...register("manufacturer")} />
            </Field>
            <Field label="Serijski broj" htmlFor="serialNumber" error={errors.serialNumber?.message}>
              <input id="serialNumber" className={inputClass} {...register("serialNumber")} />
            </Field>
          </div>

          {kind === "MjernoSredstvo" && (
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="Interval kalibracije (mjeseci)" htmlFor="interval" error={errors.calibrationIntervalMonths?.message}>
                <input id="interval" inputMode="numeric" className={inputClass} {...register("calibrationIntervalMonths")} />
              </Field>
              <Field label="Zadnja kalibracija" htmlFor="last" error={errors.lastCalibrationDate?.message}>
                <input id="last" type="date" className={inputClass} {...register("lastCalibrationDate")} />
              </Field>
              <Field label="Sljedeća kalibracija" htmlFor="next" error={errors.nextCalibrationDate?.message}
                hint={derivedNext ? `Računa se: ${formatDate(derivedNext)}` : "Unesi ručno ako nema intervala."}>
                {derivedNext ? (
                  <input id="next" className={`${inputClass} bg-slate-50`} value={formatDate(derivedNext)} readOnly />
                ) : (
                  <input id="next" type="date" className={inputClass} {...register("nextCalibrationDate")} />
                )}
              </Field>
            </div>
          )}

          <label className="flex min-h-11 items-center gap-2 text-sm">
            <input type="checkbox" className="h-5 w-5" {...register("isActive")} />
            Aktivna (nudi se u izborima)
          </label>
        </fieldset>

        <ErrorBanner error={save.error instanceof ApiError && Object.keys(save.error.fieldErrors).length ? null : save.error} onRefresh={() => void equipment.refetch()} />

        <div className="flex gap-2">
          {canEdit && (
            <button type="submit" disabled={save.isPending} className="min-h-12 rounded bg-blue-700 px-5 font-medium text-white disabled:opacity-60">
              {save.isPending ? "Spremanje…" : "Spremi"}
            </button>
          )}
          <button type="button" onClick={() => navigate("/oprema")} className="min-h-12 rounded border border-slate-300 px-5">
            {canEdit ? "Odustani" : "Nazad"}
          </button>
        </div>
      </form>
    </>
  );
}
