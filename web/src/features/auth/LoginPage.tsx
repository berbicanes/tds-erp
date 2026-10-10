import { useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Navigate, useNavigate, useSearchParams } from "react-router";
import { safeNext, useLogin, useMe } from "../../lib/auth";
import { ApiError } from "../../lib/api";

// Mirrors LoginRequestValidator on the server; the server stays the authority.
const schema = z.object({
  userName: z.string().trim().min(1, "Unesi korisničko ime."),
  password: z.string().min(1, "Unesi lozinku."),
});
type FormValues = z.infer<typeof schema>;

export function LoginPage() {
  const me = useMe();
  const login = useLogin();
  const navigate = useNavigate();
  const [params] = useSearchParams();
  const next = safeNext(params.get("next"));

  const {
    register,
    handleSubmit,
    setError,
    formState: { errors },
  } = useForm<FormValues>({ resolver: zodResolver(schema), defaultValues: { userName: "", password: "" } });

  if (me.data) return <Navigate to={next} replace />;

  const onSubmit = handleSubmit((values) =>
    login.mutate(values, {
      onSuccess: () => navigate(next, { replace: true }),
      onError: (e) => {
        if (e instanceof ApiError) {
          for (const [field, messages] of Object.entries(e.fieldErrors)) {
            if (field === "userName" || field === "password") setError(field, { message: messages[0] ?? "" });
          }
        }
      },
    }),
  );

  const serverError = login.error instanceof ApiError && Object.keys(login.error.fieldErrors).length === 0
    ? login.error.message
    : login.error && !(login.error instanceof ApiError)
      ? "Prijava nije uspjela. Pokušaj ponovo."
      : null;

  return (
    <main className="flex min-h-dvh items-center justify-center bg-slate-50 px-4">
      <form onSubmit={onSubmit} noValidate className="w-full max-w-sm space-y-4 rounded border border-slate-200 bg-white p-6">
        <h1 className="text-lg font-semibold">TDS ERP — prijava</h1>

        <div>
          <label htmlFor="userName" className="mb-1 block text-sm font-medium">
            Korisničko ime <span className="text-red-700">*</span>
          </label>
          <input
            id="userName"
            autoComplete="username"
            autoCapitalize="none"
            autoFocus
            className="h-12 w-full rounded border border-slate-300 px-3"
            aria-invalid={!!errors.userName}
            {...register("userName")}
          />
          {errors.userName && <p className="mt-1 text-sm text-red-700">{errors.userName.message}</p>}
        </div>

        <div>
          <label htmlFor="password" className="mb-1 block text-sm font-medium">
            Lozinka <span className="text-red-700">*</span>
          </label>
          <input
            id="password"
            type="password"
            autoComplete="current-password"
            className="h-12 w-full rounded border border-slate-300 px-3"
            aria-invalid={!!errors.password}
            {...register("password")}
          />
          {errors.password && <p className="mt-1 text-sm text-red-700">{errors.password.message}</p>}
        </div>

        {serverError && (
          <p role="alert" className="rounded bg-red-50 p-3 text-sm text-red-800">
            {serverError}
          </p>
        )}

        <button
          type="submit"
          disabled={login.isPending}
          className="h-12 w-full rounded bg-blue-700 font-medium text-white disabled:opacity-60"
        >
          {login.isPending ? "Prijava…" : "Prijavi se"}
        </button>
      </form>
    </main>
  );
}
