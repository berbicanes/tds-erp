import { useEffect } from "react";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useNavigate, useParams } from "react-router";
import { Field, inputClass } from "../../components/form/Field";
import { Combobox, type Option } from "../../components/form/Combobox";
import { ErrorBanner } from "../../components/ErrorBanner";
import { ApiError } from "../../lib/api";
import { optionalText, requiredText } from "../../lib/schema";
import { useUnsavedChangesPrompt } from "../../hooks/useUnsavedChangesPrompt";
import { useEmployee, useSaveEmployee, useUsers } from "./api";

// Mirrors SaveEmployeeValidator.
const schema = z.object({
  fullName: requiredText(120, "Unesi ime i prezime."),
  position: optionalText(80),
  isActive: z.boolean(),
  userId: z.string().nullable(),
});
type FormIn = z.input<typeof schema>;
type FormOut = z.output<typeof schema>;

export function EmployeeFormPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const employee = useEmployee(id);
  const users = useUsers();
  const save = useSaveEmployee(id);

  const form = useForm<FormIn, unknown, FormOut>({
    resolver: zodResolver(schema),
    defaultValues: { fullName: "", position: "", isActive: true, userId: null },
  });
  const { register, handleSubmit, reset, control, setError, formState: { errors, isDirty, isSubmitSuccessful } } = form;

  useEffect(() => {
    if (employee.data) {
      const e = employee.data;
      reset({ fullName: e.fullName, position: e.position ?? "", isActive: e.isActive, userId: e.userId });
    }
  }, [employee.data, reset]);

  const prompt = useUnsavedChangesPrompt(isDirty && !isSubmitSuccessful);

  const onSubmit = handleSubmit((values) =>
    save.mutate(
      { ...values, version: employee.data?.version ?? 0 },
      {
        onSuccess: () => navigate("/podesavanja/zaposlenici"),
        onError: (e) => {
          if (e instanceof ApiError)
            for (const [field, msgs] of Object.entries(e.fieldErrors))
              if (field in schema.shape) setError(field as keyof FormIn, { message: msgs[0] ?? "" });
        },
      },
    ),
  );

  if (id && employee.isPending) return <p className="p-4 text-slate-600">Učitavanje…</p>;
  if (id && employee.isError) return <div className="p-4"><ErrorBanner error={employee.error} /></div>;

  const linkedElsewhere = new Set((users.data ?? []).filter((u) => u.employeeId && u.employeeId !== id).map((u) => u.id));
  const userOptions: Option[] = (users.data ?? [])
    .filter((u) => !linkedElsewhere.has(u.id))
    .map((u) => (u.isActive ? { value: u.id, label: u.userName } : { value: u.id, label: u.userName, detail: "deaktiviran" }));

  return (
    <form onSubmit={onSubmit} noValidate className="max-w-xl space-y-4 px-4 py-4">
      {prompt}
      <h2 className="font-semibold">{id ? "Uredi zaposlenika" : "Novi zaposlenik"}</h2>

      <Field label="Ime i prezime" htmlFor="fullName" required error={errors.fullName?.message}>
        <input id="fullName" className={inputClass} aria-invalid={!!errors.fullName} {...register("fullName")} />
      </Field>

      <Field label="Radno mjesto" htmlFor="position" error={errors.position?.message} hint="npr. Operater lasera">
        <input id="position" className={inputClass} aria-invalid={!!errors.position} {...register("position")} />
      </Field>

      <Field label="Korisnički nalog" htmlFor="userId" error={errors.userId?.message} hint="Prazno = radnik nema login.">
        <Controller
          control={control}
          name="userId"
          render={({ field }) => (
            <Combobox id="userId" options={userOptions} value={field.value} onChange={field.onChange} allowEmpty emptyLabel="Bez korisničkog naloga" invalid={!!errors.userId} />
          )}
        />
      </Field>

      <label className="flex min-h-11 items-center gap-2 text-sm">
        <input type="checkbox" className="h-5 w-5" {...register("isActive")} />
        Aktivan (nudi se u izborima)
      </label>

      <ErrorBanner error={save.error instanceof ApiError && Object.keys(save.error.fieldErrors).length ? null : save.error} onRefresh={() => void employee.refetch()} />

      <div className="flex gap-2">
        <button type="submit" disabled={save.isPending} className="min-h-12 rounded bg-blue-700 px-5 font-medium text-white disabled:opacity-60">
          {save.isPending ? "Spremanje…" : "Spremi"}
        </button>
        <button type="button" onClick={() => navigate("/podesavanja/zaposlenici")} className="min-h-12 rounded border border-slate-300 px-5">
          Odustani
        </button>
      </div>
    </form>
  );
}
