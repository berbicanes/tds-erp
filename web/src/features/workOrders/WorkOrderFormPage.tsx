import { useState } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate, useParams } from "react-router";
import { Field, inputClass, textareaClass } from "../../components/form/Field";
import { Combobox } from "../../components/form/Combobox";
import { FilePicker } from "../../components/form/FilePicker";
import { ErrorBanner } from "../../components/ErrorBanner";
import { PageHeader } from "../../components/PageHeader";
import { ApiError } from "../../lib/api";
import { uploadFile } from "../../lib/files";
import { dateInput, intInput, optionalText, requiredText } from "../../lib/schema";
import { ChangedOnServerNotice, useUnsavedChanges } from "../../components/UnsavedChanges";
import { useServerForm } from "../../hooks/useServerForm";
import { useEmployeeOptions } from "../settings/api";
import { useSaveWorkOrder, useWorkOrder } from "./api";

// Mirrors SaveWorkOrderValidator. Status, quality approval and derived values are not in the form (golden rule 2).
const schema = z.object({
  customer: requiredText(150, "Unesi kupca."),
  productDescription: requiredText(300, "Unesi proizvod / opis."),
  orderedQty: intInput({ required: "Unesi naručenu količinu.", min: 1, minMessage: "Naručena količina mora biti veća od 0." }),
  dueDate: dateInput("Unesi rok isporuke."),
  releaseDate: dateInput(),
  priority: z.enum(["Normalan", "Hitno"]),
  drawingNumber: optionalText(60),
  drawingRevision: optionalText(20),
  responsibleEmployeeId: z.string().nullable(),
  notes: optionalText(2000),
});
type FormIn = z.input<typeof schema>;
type FormOut = z.output<typeof schema>;

export function WorkOrderFormPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const wo = useWorkOrder(id);
  const employees = useEmployeeOptions();
  const save = useSaveWorkOrder(id);
  const [drawing, setDrawing] = useState<File | null>(null);
  const [uploadError, setUploadError] = useState<unknown>(null);

  const form = useForm<FormIn, unknown, FormOut>({
    resolver: zodResolver(schema),
    defaultValues: {
      customer: "", productDescription: "", orderedQty: "", dueDate: "", releaseDate: "", priority: "Normalan",
      drawingNumber: "", drawingRevision: "", responsibleEmployeeId: null, notes: "",
    },
  });
  const { register, handleSubmit, control, setError, formState: { errors, isDirty, isSubmitting } } = form;

  // Never overwrite unsaved input with refetched data (lib/formSync.ts).
  const sync = useServerForm(form, wo.data, (w) => ({
        customer: w.customer, productDescription: w.productDescription, orderedQty: String(w.orderedQty), dueDate: w.dueDate,
        releaseDate: w.releaseDate ?? "", priority: w.priority, drawingNumber: w.drawingNumber ?? "", drawingRevision: w.drawingRevision ?? "",
        responsibleEmployeeId: w.responsibleEmployeeId, notes: w.notes ?? "",
      }));

  const markSaved = useUnsavedChanges((isDirty || !!drawing) && !save.isSuccess);
  const readOnly = wo.data ? ["Isporuceno", "Otkazano"].includes(wo.data.status) : false;

  const onSubmit = handleSubmit(async (v) => {
    setUploadError(null);
    let drawingFileId = wo.data?.drawingFileId ?? null;
    if (drawing) {
      try {
        drawingFileId = (await uploadFile(drawing)).id;
      } catch (e) {
        setUploadError(e);
        return;
      }
    }
    save.mutate({ ...v, orderedQty: v.orderedQty!, dueDate: v.dueDate!, drawingFileId, version: sync.baseVersion }, {
      onSuccess: (saved) => { markSaved(); navigate(`/nalozi/${saved.id}`); },
      onError: (e) => {
        if (e instanceof ApiError) for (const [f, m] of Object.entries(e.fieldErrors)) setError(f as keyof FormIn, { message: m[0] ?? "" });
      },
    });
  });

  if (id && wo.isPending) return <p className="p-4 text-slate-600">Učitavanje…</p>;
  if (id && wo.isError) return <div className="p-4"><ErrorBanner error={wo.error} /></div>;

  return (
    <>
      <PageHeader title={id ? `Uredi nalog ${wo.data?.number ?? ""}` : "Novi nalog"} />
      <form onSubmit={onSubmit} noValidate className="max-w-3xl space-y-4 px-4 py-4">
        {readOnly && <p className="rounded bg-zinc-100 p-3 text-sm">Nalog je {wo.data?.status === "Otkazano" ? "otkazan" : "isporučen"}. Može se mijenjati samo napomena.</p>}
        {!id && <p className="text-sm text-slate-600">Broj naloga (RN-…) dodjeljuje sistem pri spremanju.</p>}
        <fieldset disabled={readOnly} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Kupac" htmlFor="customer" required error={errors.customer?.message}>
              <input id="customer" className={inputClass} aria-invalid={!!errors.customer} {...register("customer")} />
            </Field>
            <Field label="Prioritet" htmlFor="priority" required>
              <select id="priority" className={inputClass} {...register("priority")}>
                <option value="Normalan">Normalan</option>
                <option value="Hitno">Hitno</option>
              </select>
            </Field>
          </div>
          <Field label="Proizvod / opis" htmlFor="productDescription" required error={errors.productDescription?.message}>
            <input id="productDescription" className={inputClass} aria-invalid={!!errors.productDescription} {...register("productDescription")} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Naručena količina (kom)" htmlFor="orderedQty" required error={errors.orderedQty?.message}>
              <input id="orderedQty" inputMode="numeric" className={inputClass} aria-invalid={!!errors.orderedQty} {...register("orderedQty")} />
            </Field>
            <Field label="Rok isporuke" htmlFor="dueDate" required error={errors.dueDate?.message}>
              <input id="dueDate" type="date" className={inputClass} aria-invalid={!!errors.dueDate} {...register("dueDate")} />
            </Field>
            <Field label="Datum puštanja" htmlFor="releaseDate" error={errors.releaseDate?.message}>
              <input id="releaseDate" type="date" className={inputClass} {...register("releaseDate")} />
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Broj crteža" htmlFor="drawingNumber" error={errors.drawingNumber?.message}>
              <input id="drawingNumber" className={inputClass} {...register("drawingNumber")} />
            </Field>
            <Field label="Revizija crteža" htmlFor="drawingRevision" error={errors.drawingRevision?.message}>
              <input id="drawingRevision" className={inputClass} {...register("drawingRevision")} />
            </Field>
            <Field label="Odgovorna osoba" htmlFor="responsibleEmployeeId">
              <Controller control={control} name="responsibleEmployeeId" render={({ field }) => (
                <Combobox id="responsibleEmployeeId" value={field.value} onChange={field.onChange} allowEmpty
                  options={(employees.data ?? []).map((e) => ({ value: e.id, label: e.fullName }))} />
              )} />
            </Field>
          </div>
          <Field label={wo.data?.drawingFileName ? `Važeći crtež: ${wo.data.drawingFileName} (zamijeni)` : "Važeći crtež"} htmlFor="drawing">
            <FilePicker file={drawing} onChange={setDrawing} label="Odaberi crtež (PDF, DXF, DWG, STEP…)" />
          </Field>
        </fieldset>
        <Field label="Napomena" htmlFor="notes" error={errors.notes?.message}>
          <textarea id="notes" className={textareaClass} {...register("notes")} />
        </Field>

        <ChangedOnServerNotice show={sync.changedOnServer} onDiscard={sync.discard} />
        <ErrorBanner error={uploadError ?? (save.error instanceof ApiError && Object.keys(save.error.fieldErrors).length ? null : save.error)} onRefresh={() => { save.reset(); void sync.reload(wo.refetch); }} />

        <div className="flex gap-2">
          <button type="submit" disabled={save.isPending || isSubmitting} className="min-h-12 rounded bg-blue-700 px-5 font-medium text-white disabled:opacity-60">
            {save.isPending || isSubmitting ? "Spremanje…" : "Spremi"}
          </button>
          <button type="button" onClick={() => navigate(id ? `/nalozi/${id}` : "/nalozi")} className="min-h-12 rounded border border-slate-300 px-5">Odustani</button>
        </div>
      </form>
    </>
  );
}
