import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate } from "react-router";
import { Field, inputClass } from "../../components/form/Field";
import { Combobox } from "../../components/form/Combobox";
import { ErrorBanner } from "../../components/ErrorBanner";
import { ApiError } from "../../lib/api";
import { labelOf, supplierApprovalStatus } from "../../lib/labels";
import { decimalInput, optionalText, requiredText, dateInput } from "../../lib/schema";
import { itemTitle } from "../../lib/material";
import { useUnsavedChangesPrompt } from "../../hooks/useUnsavedChangesPrompt";
import { useMaterialItemOptions } from "./itemsApi";
import { useSupplierOptions } from "../suppliers/api";
import { useReceiveLot } from "./lotsApi";
import { todaySarajevo as todayIso } from "../../lib/date";

// Mirrors ReceiveLotValidator.
const schema = z.object({
  lotNumber: requiredText(60, "Unesi oznaku lota / heat broj sa certifikata."),
  materialItemId: z.string({ message: "Odaberi artikal." }).nullable().refine((v) => !!v, "Odaberi artikal."),
  supplierId: z.string().nullable().refine((v) => !!v, "Odaberi dobavljača."),
  receivedDate: dateInput("Unesi datum prijema."),
  receivedQty: decimalInput({ required: "Unesi primljenu količinu.", min: 0.001, minMessage: "Primljena količina mora biti veća od 0." }),
  dimensionsDescription: optionalText(200),
  storageLocation: optionalText(60),
});
type FormIn = z.input<typeof schema>;
type FormOut = z.output<typeof schema>;


export function ReceiveLotPage() {
  const navigate = useNavigate();
  const items = useMaterialItemOptions();
  const suppliers = useSupplierOptions();
  const receive = useReceiveLot();

  const { register, handleSubmit, control, watch, setError, formState: { errors, isDirty, isSubmitSuccessful } } = useForm<FormIn, unknown, FormOut>({
    resolver: zodResolver(schema),
    defaultValues: { lotNumber: "", materialItemId: null, supplierId: null, receivedDate: todayIso(), receivedQty: "", dimensionsDescription: "", storageLocation: "" },
  });
  const prompt = useUnsavedChangesPrompt(isDirty && !isSubmitSuccessful);

  const selectedItem = items.data?.find((i) => i.id === watch("materialItemId"));
  const selectedSupplier = suppliers.data?.find((s) => s.id === watch("supplierId"));

  const onSubmit = handleSubmit((v) =>
    receive.mutate(
      { ...v, materialItemId: v.materialItemId!, supplierId: v.supplierId!, receivedDate: v.receivedDate!, receivedQty: v.receivedQty! },
      {
        onSuccess: (r) => navigate(`/zalihe/lotovi/${r.data.id}`, { state: { warnings: r.warnings } }),
        onError: (e) => {
          if (e instanceof ApiError)
            for (const [f, m] of Object.entries(e.fieldErrors)) setError(f as keyof FormIn, { message: m[0] ?? "" });
        },
      },
    ),
  );

  const itemOptions = (items.data ?? []).map((i) => ({ value: i.id, label: itemTitle(i), detail: i.unit }));
  const supplierOptions = (suppliers.data ?? []).map((s) =>
    s.approvalStatus === "Odobren" ? { value: s.id, label: s.name } : { value: s.id, label: s.name, detail: labelOf(supplierApprovalStatus, s.approvalStatus).text });

  return (
    <form onSubmit={onSubmit} noValidate className="max-w-2xl space-y-4 px-4 py-4">
      {prompt}
      <h2 className="font-semibold">Prijem lota</h2>
      <Field label="Oznaka lota / heat broj" htmlFor="lotNumber" required error={errors.lotNumber?.message} hint="Upiši heat broj sa certifikata.">
        <input id="lotNumber" autoCapitalize="characters" className={inputClass} aria-invalid={!!errors.lotNumber} {...register("lotNumber")} />
      </Field>
      <Field label="Artikal" htmlFor="materialItemId" required error={errors.materialItemId?.message}>
        <Controller control={control} name="materialItemId" render={({ field }) => (
          <Combobox id="materialItemId" options={itemOptions} value={field.value} onChange={field.onChange} invalid={!!errors.materialItemId} placeholder="Npr. Lim crni S235" />
        )} />
      </Field>
      <Field label="Dobavljač" htmlFor="supplierId" required error={errors.supplierId?.message}
        hint={selectedSupplier?.approvalStatus === "Blokiran" ? "Dobavljač je blokiran — prijem neće biti dozvoljen." : undefined}>
        <Controller control={control} name="supplierId" render={({ field }) => (
          <Combobox id="supplierId" options={supplierOptions} value={field.value} onChange={field.onChange} invalid={!!errors.supplierId} />
        )} />
      </Field>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Datum prijema" htmlFor="receivedDate" required error={errors.receivedDate?.message}>
          <input id="receivedDate" type="date" max={todayIso()} className={inputClass} {...register("receivedDate")} />
        </Field>
        <Field label={`Primljena količina${selectedItem ? ` (${selectedItem.unit})` : ""}`} htmlFor="receivedQty" required error={errors.receivedQty?.message}
          hint="Količina se kasnije ne mijenja; greška se ispravlja korekcijom.">
          <input id="receivedQty" inputMode="decimal" className={inputClass} aria-invalid={!!errors.receivedQty} {...register("receivedQty")} />
        </Field>
      </div>
      <div className="grid gap-4 sm:grid-cols-2">
        <Field label="Dimenzije / količina (opis)" htmlFor="dimensionsDescription" error={errors.dimensionsDescription?.message} hint="npr. 3000×1500, 10 tabli">
          <input id="dimensionsDescription" className={inputClass} {...register("dimensionsDescription")} />
        </Field>
        <Field label="Skladišna lokacija" htmlFor="storageLocation" error={errors.storageLocation?.message}>
          <input id="storageLocation" className={inputClass} {...register("storageLocation")} />
        </Field>
      </div>
      <p className="text-sm text-slate-600">Lot se prima sa odlukom „Na kontroli”. Odluka prijema i certifikat se unose na detalju lota.</p>
      <ErrorBanner error={receive.error instanceof ApiError && Object.keys(receive.error.fieldErrors).length ? null : receive.error} />
      <div className="flex gap-2">
        <button type="submit" disabled={receive.isPending} className="min-h-12 rounded bg-blue-700 px-5 font-medium text-white disabled:opacity-60">
          {receive.isPending ? "Spremanje…" : "Primi lot"}
        </button>
        <button type="button" onClick={() => navigate("/zalihe/lotovi")} className="min-h-12 rounded border border-slate-300 px-5">Odustani</button>
      </div>
    </form>
  );
}
