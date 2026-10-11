import { useState } from "react";
import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Field, inputClass } from "../../components/form/Field";
import { ErrorBanner } from "../../components/ErrorBanner";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { ApiError } from "../../lib/api";
import { useMe, type Role } from "../../lib/auth";
import { requiredText } from "../../lib/schema";
import { useCreateUser, useSetPassword, useUpdateUser, useUsers, type User } from "./api";
import { useUnsavedChanges } from "../../components/UnsavedChanges";

const ROLES: { value: Role; text: string }[] = [
  { value: "Operater", text: "Operater" },
  { value: "Kontrolor", text: "Kontrolor" },
  { value: "Admin", text: "Admin" },
];

// Mirrors CreateUserValidator + password policy (DOMAIN §8).
const passwordSchema = z
  .string()
  .min(10, "Lozinka mora imati najmanje 10 znakova.")
  .regex(/\d/, "Lozinka mora imati barem jednu cifru.")
  .regex(/\p{Ll}/u, "Lozinka mora imati barem jedno malo slovo.");

const createSchema = z.object({
  userName: requiredText(50, "Unesi korisničko ime.").regex(/^[a-zA-Z0-9._-]+$/, "Samo slova bez dijakritika, cifre, tačka, crtica i donja crta."),
  role: z.enum(["Operater", "Kontrolor", "Admin"]),
  password: passwordSchema,
});
type CreateIn = z.input<typeof createSchema>;

function CreateUserForm() {
  const create = useCreateUser();
  const { register, handleSubmit, reset, setError, formState: { errors, isDirty } } = useForm<CreateIn>({
    resolver: zodResolver(createSchema),
    defaultValues: { userName: "", role: "Operater", password: "" },
  });
  useUnsavedChanges(isDirty);
  const onSubmit = handleSubmit((v) =>
    create.mutate(v, {
      onSuccess: () => reset(),
      onError: (e) => {
        if (e instanceof ApiError)
          for (const [f, m] of Object.entries(e.fieldErrors))
            if (f === "userName" || f === "password" || f === "role") setError(f, { message: m[0] ?? "" });
      },
    }),
  );
  return (
    <form onSubmit={onSubmit} noValidate className="grid gap-3 rounded border border-slate-200 p-3 sm:grid-cols-4 sm:items-start">
      <Field label="Korisničko ime" htmlFor="new-userName" required error={errors.userName?.message}>
        <input id="new-userName" autoCapitalize="none" className={inputClass} aria-invalid={!!errors.userName} {...register("userName")} />
      </Field>
      <Field label="Uloga" htmlFor="new-role" required>
        <select id="new-role" className={inputClass} {...register("role")}>
          {ROLES.map((r) => <option key={r.value} value={r.value}>{r.text}</option>)}
        </select>
      </Field>
      <Field label="Lozinka" htmlFor="new-password" required error={errors.password?.message} hint="Min. 10 znakova, cifra i malo slovo.">
        <input id="new-password" type="password" autoComplete="new-password" className={inputClass} aria-invalid={!!errors.password} {...register("password")} />
      </Field>
      <div className="sm:pt-6">
        <button type="submit" disabled={create.isPending} className="min-h-11 w-full rounded bg-blue-700 px-4 text-sm font-medium text-white disabled:opacity-60">
          Dodaj korisnika
        </button>
      </div>
      {create.error && !(create.error instanceof ApiError && Object.keys(create.error.fieldErrors).length) && (
        <div className="sm:col-span-4"><ErrorBanner error={create.error} /></div>
      )}
    </form>
  );
}

function PasswordDialog({ user, onClose }: { user: User | null; onClose: () => void }) {
  const setPassword = useSetPassword();
  const [value, setValue] = useState("");
  const [error, setErrorText] = useState<string | null>(null);
  const [done, setDone] = useState(false);
  useUnsavedChanges(!!user && value !== "" && !done);

  const close = () => {
    setValue("");
    setErrorText(null);
    setDone(false);
    setPassword.reset();
    onClose();
  };

  return (
    <ConfirmDialog
      open={!!user}
      title={`Nova lozinka za ${user?.userName ?? ""}`}
      confirmText={done ? "Zatvori" : "Postavi lozinku"}
      onCancel={close}
      onConfirm={() => {
        if (done) return close();
        const parsed = passwordSchema.safeParse(value);
        if (!parsed.success) return setErrorText(parsed.error.issues[0]?.message ?? "");
        setPassword.mutate({ id: user!.id, password: value }, {
          onSuccess: () => setDone(true),
          onError: (e) => setErrorText(e.message),
        });
      }}
    >
      {done ? (
        <p>Lozinka je promijenjena. Javi je korisniku lično.</p>
      ) : (
        <div className="space-y-1">
          <label htmlFor="reset-password" className="block text-sm font-medium">Nova lozinka</label>
          <input id="reset-password" type="password" autoComplete="new-password" className={inputClass} value={value} onChange={(e) => setValue(e.target.value)} />
          {error && <p className="text-sm text-red-700">{error}</p>}
        </div>
      )}
    </ConfirmDialog>
  );
}

export function UsersPage() {
  const users = useUsers();
  const update = useUpdateUser();
  const me = useMe();
  const [passwordFor, setPasswordFor] = useState<User | null>(null);

  return (
    <div className="space-y-4 px-4 py-3">
      <CreateUserForm />
      <ErrorBanner error={users.error ?? update.error} onRefresh={() => void users.refetch()} />
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="w-full text-sm cards-sm">
          <thead className="bg-slate-50 text-left text-xs text-slate-600">
            <tr>
              <th className="px-2 py-2 font-medium">Korisničko ime</th>
              <th className="px-2 py-2 font-medium">Uloga</th>
              <th className="px-2 py-2 font-medium">Zaposlenik</th>
              <th className="px-2 py-2 font-medium">Status</th>
              <th className="px-2 py-2 font-medium"><span className="sr-only">Akcije</span></th>
            </tr>
          </thead>
          <tbody>
            {users.isPending && <tr><td colSpan={5} className="px-2 py-6 text-center text-slate-500">Učitavanje…</td></tr>}
            {users.data?.map((u) => {
              const self = u.id === me.data?.id;
              return (
                <tr key={u.id} className="border-t border-slate-100">
                  <td data-label="Korisničko ime" className="px-2 py-1.5 font-medium">{u.userName}</td>
                  <td data-label="Uloga" className="px-2 py-1.5">
                    <select
                      aria-label={`Uloga za ${u.userName}`}
                      value={u.role}
                      disabled={self || update.isPending}
                      onChange={(e) => update.mutate({ id: u.id, role: e.target.value as Role, isActive: u.isActive })}
                      className="h-9 rounded border border-slate-300 px-2"
                    >
                      {ROLES.map((r) => <option key={r.value} value={r.value}>{r.text}</option>)}
                    </select>
                  </td>
                  <td data-label="Zaposlenik" className="px-2 py-1.5">{u.employeeName ?? "Nije vezan"}</td>
                  <td data-label="Status" className="px-2 py-1.5">{u.isActive ? "Aktivan" : "Deaktiviran"}</td>
                  <td className="space-x-2 whitespace-nowrap px-2 py-1.5 text-right">
                    <button type="button" onClick={() => setPasswordFor(u)} className="min-h-9 rounded border border-slate-300 px-2">
                      Nova lozinka
                    </button>
                    {!self && (
                      <button
                        type="button"
                        disabled={update.isPending}
                        onClick={() => update.mutate({ id: u.id, role: u.role, isActive: !u.isActive })}
                        className="min-h-9 rounded border border-slate-300 px-2"
                      >
                        {u.isActive ? "Deaktiviraj" : "Aktiviraj"}
                      </button>
                    )}
                  </td>
                </tr>
              );
            })}
          </tbody>
        </table>
      </div>
      <p className="text-xs text-slate-500">Korisnici se ne brišu. Deaktiviran korisnik se ne može prijaviti. Zaposlenik se veže za nalog u tabu Zaposlenici.</p>
      <PasswordDialog user={passwordFor} onClose={() => setPasswordFor(null)} />
    </div>
  );
}
