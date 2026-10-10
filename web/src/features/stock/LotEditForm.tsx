import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Field, inputClass } from "../../components/form/Field";
import { Combobox } from "../../components/form/Combobox";
import { ErrorBanner } from "../../components/ErrorBanner";
import { ApiError } from "../../lib/api";
import { optionalText, requiredText } from "../../lib/schema";
import { useSupplierOptions } from "../suppliers/api";
import { useUpdateLot, type Lot } from "./lotsApi";

// Mirrors UpdateLotValidator. Item, received date and quantity are not editable (DOMAIN §2.2).
const schema = z.object({
  lotNumber: requiredText(60, "Unesi oznaku lota / heat broj sa certifikata."),
  supplierId: z.string().nullable().refine((v) => !!v, "Odaberi dobavljača."),
  dimensionsDescription: optionalText(200),
  storageLocation: optionalText(60),
});
type FormIn = z.input<typeof schema>;
type FormOut = z.output<typeof schema>;

export function LotEditForm({ lot, onDone }: { lot: Lot; onDone: () => void }) {
  const update = useUpdateLot(lot.id);
  const suppliers = useSupplierOptions();
  const { register, handleSubmit, control, setError, formState: { errors } } = useForm<FormIn, unknown, FormOut>({
    resolver: zodResolver(schema),
    defaultValues: { lotNumber: lot.lotNumber, supplierId: lot.supplierId, dimensionsDescription: lot.dimensionsDescription ?? "", storageLocation: lot.storageLocation ?? "" },
  });
  const onSubmit = handleSubmit((v) =>
    update.mutate({ ...v, supplierId: v.supplierId!, version: lot.version }, {
      onSuccess: onDone,
      onError: (e) => {
        if (e instanceof ApiError) for (const [f, m] of Object.entries(e.fieldErrors)) setError(f as keyof FormIn, { message: m[0] ?? "" });
      },
    }),
  );
  return (
    <form onSubmit={onSubmit} noValidate className="max-w-xl space-y-3 rounded border border-slate-200 p-3">
      <Field label="Oznaka lota / heat broj" htmlFor="edit-lotNumber" required error={errors.lotNumber?.message}>
        <input id="edit-lotNumber" className={inputClass} {...register("lotNumber")} />
      </Field>
      <Field label="Dobavljač" htmlFor="edit-supplierId" required error={errors.supplierId?.message}>
        <Controller control={control} name="supplierId" render={({ field }) => (
          <Combobox id="edit-supplierId" value={field.value} onChange={field.onChange} options={(suppliers.data ?? []).map((s) => ({ value: s.id, label: s.name }))} />
        )} />
      </Field>
      <div className="grid gap-3 sm:grid-cols-2">
        <Field label="Dimenzije / količina (opis)" htmlFor="edit-dim" error={errors.dimensionsDescription?.message}>
          <input id="edit-dim" className={inputClass} {...register("dimensionsDescription")} />
        </Field>
        <Field label="Skladišna lokacija" htmlFor="edit-loc" error={errors.storageLocation?.message}>
          <input id="edit-loc" className={inputClass} {...register("storageLocation")} />
        </Field>
      </div>
      <ErrorBanner error={update.error instanceof ApiError && Object.keys(update.error.fieldErrors).length ? null : update.error} />
      <div className="flex gap-2">
        <button type="submit" disabled={update.isPending} className="min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white">Spremi</button>
        <button type="button" onClick={onDone} className="min-h-11 rounded border border-slate-300 px-4 text-sm">Odustani</button>
      </div>
    </form>
  );
}
