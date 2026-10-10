import { useEffect } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate, useParams } from "react-router";
import { Field, inputClass, textareaClass } from "../../components/form/Field";
import { Combobox } from "../../components/form/Combobox";
import { ErrorBanner } from "../../components/ErrorBanner";
import { PageHeader } from "../../components/PageHeader";
import { ApiError } from "../../lib/api";
import { hasRole, useMe } from "../../lib/auth";
import { supplierApprovalStatus } from "../../lib/labels";
import { dateInput, optionalText, requiredText } from "../../lib/schema";
import { useUnsavedChangesPrompt } from "../../hooks/useUnsavedChangesPrompt";
import { useEmployeeOptions } from "../settings/api";
import { useSaveSupplier, useSupplier } from "./api";

// Mirrors SaveSupplierValidator.
const schema = z
  .object({
    name: requiredText(150, "Unesi naziv dobavljača."),
    materialOrService: optionalText(300),
    approvalStatus: z.enum(["UProcjeni", "Odobren", "UslovnoOdobren", "Blokiran"]),
    initialEvaluationDate: dateInput(),
    nextEvaluationDate: dateInput(),
    evaluationCriteria: optionalText(2000),
    responsibleEmployeeId: z.string().nullable(),
    notes: optionalText(2000),
  })
  .refine((v) => !v.initialEvaluationDate || !v.nextEvaluationDate || v.nextEvaluationDate >= v.initialEvaluationDate, {
    path: ["nextEvaluationDate"],
    message: "Sljedeća ocjena ne može biti prije početne ocjene.",
  });
type FormIn = z.input<typeof schema>;
type FormOut = z.output<typeof schema>;

export function SupplierFormPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const me = useMe();
  const canEdit = hasRole(me.data, "Admin", "Kontrolor");
  const supplier = useSupplier(id);
  const employees = useEmployeeOptions();
  const save = useSaveSupplier(id);

  const { register, handleSubmit, reset, control, watch, setError, formState: { errors, isDirty, isSubmitSuccessful } } = useForm<FormIn, unknown, FormOut>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: "", materialOrService: "", approvalStatus: "UProcjeni", initialEvaluationDate: "", nextEvaluationDate: "",
      evaluationCriteria: "", responsibleEmployeeId: null, notes: "",
    },
  });

  useEffect(() => {
    const s = supplier.data;
    if (s)
      reset({
        name: s.name, materialOrService: s.materialOrService ?? "", approvalStatus: s.approvalStatus,
        initialEvaluationDate: s.initialEvaluationDate ?? "", nextEvaluationDate: s.nextEvaluationDate ?? "",
        evaluationCriteria: s.evaluationCriteria ?? "", responsibleEmployeeId: s.responsibleEmployeeId, notes: s.notes ?? "",
      });
  }, [supplier.data, reset]);

  const prompt = useUnsavedChangesPrompt(canEdit && isDirty && !isSubmitSuccessful);
  const blocked = watch("approvalStatus") === "Blokiran";

  const onSubmit = handleSubmit((values) =>
    save.mutate({ ...values, version: supplier.data?.version ?? 0 }, {
      onSuccess: () => navigate("/dobavljaci"),
      onError: (e) => {
        if (e instanceof ApiError)
          for (const [f, m] of Object.entries(e.fieldErrors)) setError(f as keyof FormIn, { message: m[0] ?? "" });
      },
    }),
  );

  if (id && supplier.isPending) return <p className="p-4 text-slate-600">Učitavanje…</p>;
  if (id && supplier.isError) return <div className="p-4"><ErrorBanner error={supplier.error} /></div>;

  const employeeOptions = (employees.data ?? []).map((e) => (e.position ? { value: e.id, label: e.fullName, detail: e.position } : { value: e.id, label: e.fullName }));

  return (
    <>
      <PageHeader title={id ? supplier.data?.name ?? "Dobavljač" : "Novi dobavljač"} />
      <form onSubmit={onSubmit} noValidate className="max-w-2xl space-y-4 px-4 py-4">
        {prompt}
        <fieldset disabled={!canEdit} className="space-y-4">
          <Field label="Naziv dobavljača" htmlFor="name" required error={errors.name?.message}>
            <input id="name" className={inputClass} aria-invalid={!!errors.name} {...register("name")} />
          </Field>
          <Field label="Materijal / usluga" htmlFor="materialOrService" error={errors.materialOrService?.message}>
            <input id="materialOrService" className={inputClass} {...register("materialOrService")} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Status odobrenja" htmlFor="approvalStatus" required>
              <select id="approvalStatus" className={inputClass} {...register("approvalStatus")}>
                {Object.entries(supplierApprovalStatus).map(([v, l]) => <option key={v} value={v}>{l.text}</option>)}
              </select>
            </Field>
            <Field label="Početna ocjena" htmlFor="initialEvaluationDate" error={errors.initialEvaluationDate?.message}>
              <input id="initialEvaluationDate" type="date" className={inputClass} {...register("initialEvaluationDate")} />
            </Field>
            <Field label="Sljedeća ocjena" htmlFor="nextEvaluationDate" error={errors.nextEvaluationDate?.message}>
              <input id="nextEvaluationDate" type="date" className={inputClass} aria-invalid={!!errors.nextEvaluationDate} {...register("nextEvaluationDate")} />
            </Field>
          </div>
          <Field label="Odgovorna osoba" htmlFor="responsibleEmployeeId">
            <Controller control={control} name="responsibleEmployeeId" render={({ field }) => (
              <Combobox id="responsibleEmployeeId" options={employeeOptions} value={field.value} onChange={field.onChange} allowEmpty />
            )} />
          </Field>
          <Field label="Kriteriji / rezultat ocjene" htmlFor="evaluationCriteria" error={errors.evaluationCriteria?.message}>
            <textarea id="evaluationCriteria" className={textareaClass} {...register("evaluationCriteria")} />
          </Field>
          <Field label="Napomene" htmlFor="notes" error={errors.notes?.message}
            hint={blocked ? "Upiši razlog blokade, npr. da se dobavljač više ne koristi." : undefined}>
            <textarea id="notes" className={textareaClass} {...register("notes")} />
          </Field>
        </fieldset>

        <ErrorBanner error={save.error instanceof ApiError && Object.keys(save.error.fieldErrors).length ? null : save.error} onRefresh={() => void supplier.refetch()} />

        <div className="flex gap-2">
          {canEdit && (
            <button type="submit" disabled={save.isPending} className="min-h-12 rounded bg-blue-700 px-5 font-medium text-white disabled:opacity-60">
              {save.isPending ? "Spremanje…" : "Spremi"}
            </button>
          )}
          <button type="button" onClick={() => navigate("/dobavljaci")} className="min-h-12 rounded border border-slate-300 px-5">
            {canEdit ? "Odustani" : "Nazad"}
          </button>
        </div>
      </form>
    </>
  );
}
