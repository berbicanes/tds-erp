/**
 * UI §4: − / number / + for gloves in the hall; the number can also be typed (inputmode numeric).
 * Buttons are 48 px. Empty input = not entered.
 */
export function Stepper({ id, label, value, onChange, step = 1, decimals = false, disabled, hint }: {
  id: string;
  label: string;
  value: string;
  onChange: (value: string) => void;
  step?: number;
  decimals?: boolean;
  disabled?: boolean;
  hint?: string | undefined;
}) {
  const current = Number(value.replace(",", ".")) || 0;
  const set = (n: number) => onChange(n <= 0 ? "" : decimals ? String(n).replace(".", ",") : String(Math.round(n)));
  const btn = "flex h-12 w-12 shrink-0 items-center justify-center rounded border border-slate-300 bg-white text-2xl font-medium disabled:opacity-40";
  return (
    <div>
      <label htmlFor={id} className="mb-1 block text-sm font-medium">{label}</label>
      <div className="flex items-center gap-2">
        <button type="button" className={btn} disabled={disabled || current <= 0} onClick={() => set(current - step)} aria-label={`${label}: manje`}>−</button>
        <input id={id} value={value} disabled={disabled} inputMode={decimals ? "decimal" : "numeric"} autoComplete="off"
          onChange={(e) => onChange(e.target.value.replace(decimals ? /[^\d,.]/g : /\D/g, ""))}
          className="h-12 w-full min-w-0 rounded border border-slate-300 bg-white px-2 text-center text-lg tabular-nums disabled:bg-slate-100" />
        <button type="button" className={btn} disabled={disabled} onClick={() => set(current + step)} aria-label={`${label}: više`}>+</button>
      </div>
      {hint && <p className="mt-1 text-xs text-slate-600">{hint}</p>}
    </div>
  );
}
