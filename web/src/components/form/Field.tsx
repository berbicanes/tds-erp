import type { ReactNode } from "react";

/** UI §7: label above, required marked with *, error below in red. */
export function Field({ label, htmlFor, required, error, hint, children }: {
  label: string;
  htmlFor: string;
  required?: boolean;
  error?: string | undefined;
  hint?: string;
  children: ReactNode;
}) {
  return (
    <div>
      <label htmlFor={htmlFor} className="mb-1 block text-sm font-medium">
        {label} {required && <span className="text-red-700">*</span>}
      </label>
      {children}
      {hint && !error && <p className="mt-1 text-xs text-slate-500">{hint}</p>}
      {error && <p className="mt-1 text-sm text-red-700">{error}</p>}
    </div>
  );
}

export const inputClass =
  "h-11 w-full rounded border border-slate-300 bg-white px-3 text-sm aria-[invalid=true]:border-red-600";
export const textareaClass =
  "min-h-20 w-full rounded border border-slate-300 bg-white px-3 py-2 text-sm aria-[invalid=true]:border-red-600";
