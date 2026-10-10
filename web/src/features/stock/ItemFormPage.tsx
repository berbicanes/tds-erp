import { useEffect } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate, useParams } from "react-router";
import { Field, inputClass } from "../../components/form/Field";
import { ErrorBanner } from "../../components/ErrorBanner";
import { ApiError } from "../../lib/api";
import { hasRole, useMe } from "../../lib/auth";
import { unit as unitLabels } from "../../lib/labels";
import { dateInput, decimalInput, optionalText, requiredText, toInput } from "../../lib/schema";
import { itemTitle } from "../../lib/material";
import { useUnsavedChangesPrompt } from "../../hooks/useUnsavedChangesPrompt";
import { useMaterialItem, useSaveMaterialItem } from "./itemsApi";

// Mirrors SaveMaterialItemValidator.
const schema = z.object({
  name: requiredText(120, "Unesi naziv artikla, npr. Lim crni."),
  grade: optionalText(40),
  thicknessMm: decimalInput({ min: 0.01, minMessage: "Debljina mora biti veća od 0." }),
  unit: z.enum(["kg", "kom", "m", "tabla", "drugo"]),
  minimumStock: decimalInput({ min: 0, minMessage: "Minimalna zaliha ne može biti negativna." }),
  nextStocktakeDate: dateInput(),
  isActive: z.boolean(),
});
type FormIn = z.input<typeof schema>;
type FormOut = z.output<typeof schema>;

export function ItemFormPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const me = useMe();
  const canEdit = hasRole(me.data, "Admin", "Kontrolor");
  const item = useMaterialItem(id);
  const save = useSaveMaterialItem(id);

  const { register, handleSubmit, reset, setError, formState: { errors, isDirty, isSubmitSuccessful } } = useForm<FormIn, unknown, FormOut>({
    resolver: zodResolver(schema),
    defaultValues: { name: "", grade: "", thicknessMm: "", unit: "tabla", minimumStock: "", nextStocktakeDate: "", isActive: true },
  });

  useEffect(() => {
    const m = item.data;
    if (m)
      reset({
        name: m.name, grade: m.grade ?? "", thicknessMm: toInput(m.thicknessMm), unit: m.unit,
        minimumStock: toInput(m.minimumStock), nextStocktakeDate: m.nextStocktakeDate ?? "", isActive: m.isActive,
      });
  }, [item.data, reset]);

  const prompt = useUnsavedChangesPrompt(canEdit && isDirty && !isSubmitSuccessful);

  const onSubmit = handleSubmit((values) =>
    save.mutate({ ...values, version: item.data?.version ?? 0 }, {
      onSuccess: () => navigate("/zalihe/artikli"),
      onError: (e) => {
        if (e instanceof ApiError)
          for (const [f, m] of Object.entries(e.fieldErrors)) setError(f as keyof FormIn, { message: m[0] ?? "" });
      },
    }),
  );

  if (id && item.isPending) return <p className="p-4 text-slate-600">Učitavanje…</p>;
  if (id && item.isError) return <div className="p-4"><ErrorBanner error={item.error} /></div>;

  return (
    <form onSubmit={onSubmit} noValidate className="max-w-xl space-y-4 px-4 py-4">
      {prompt}
      <h2 className="font-semibold">{item.data ? itemTitle(item.data) : "Novi artikal"}</h2>
      <fieldset disabled={!canEdit} className="space-y-4">
        <Field label="Materijal / artikal" htmlFor="name" required error={errors.name?.message} hint="npr. Lim crni, Lim inox, Lim aluminij">
          <input id="name" className={inputClass} aria-invalid={!!errors.name} {...register("name")} />
        </Field>
        <div className="grid gap-4 sm:grid-cols-3">
          <Field label="Kvalitet / oznaka" htmlFor="grade" error={errors.grade?.message} hint="npr. S235, 1.4301, AlMg3">
            <input id="grade" className={inputClass} {...register("grade")} />
          </Field>
          <Field label="Debljina mm" htmlFor="thicknessMm" error={errors.thicknessMm?.message}>
            <input id="thicknessMm" inputMode="decimal" className={inputClass} aria-invalid={!!errors.thicknessMm} {...register("thicknessMm")} />
          </Field>
          <Field label="Jedinica" htmlFor="unit" required error={errors.unit?.message}>
            <select id="unit" className={inputClass} {...register("unit")}>
              {Object.entries(unitLabels).map(([v, l]) => <option key={v} value={v}>{l.text}</option>)}
            </select>
          </Field>
        </div>
        <div className="grid gap-4 sm:grid-cols-2">
          <Field label="Minimalna zaliha" htmlFor="minimumStock" error={errors.minimumStock?.message} hint="Prazno = nivo zalihe se ne računa (Nije definisano).">
            <input id="minimumStock" inputMode="decimal" className={inputClass} aria-invalid={!!errors.minimumStock} {...register("minimumStock")} />
          </Field>
          <Field label="Datum sljedećeg popisa" htmlFor="nextStocktakeDate" error={errors.nextStocktakeDate?.message}>
            <input id="nextStocktakeDate" type="date" className={inputClass} {...register("nextStocktakeDate")} />
          </Field>
        </div>
        <label className="flex min-h-11 items-center gap-2 text-sm">
          <input type="checkbox" className="h-5 w-5" {...register("isActive")} />
          Aktivan (nudi se pri prijemu lota)
        </label>
      </fieldset>

      <ErrorBanner error={save.error instanceof ApiError && Object.keys(save.error.fieldErrors).length ? null : save.error} onRefresh={() => void item.refetch()} />

      <div className="flex gap-2">
        {canEdit && (
          <button type="submit" disabled={save.isPending} className="min-h-12 rounded bg-blue-700 px-5 font-medium text-white disabled:opacity-60">
            {save.isPending ? "Spremanje…" : "Spremi"}
          </button>
        )}
        <button type="button" onClick={() => navigate("/zalihe/artikli")} className="min-h-12 rounded border border-slate-300 px-5">
          {canEdit ? "Odustani" : "Nazad"}
        </button>
      </div>
    </form>
  );
}
