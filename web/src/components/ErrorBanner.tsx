import { ApiError } from "../lib/api";

/** Server error shown above the action (UI §4, §9). 409 edit conflicts get a "Osvježi" button. */
export function ErrorBanner({ error, onRefresh }: { error: unknown; onRefresh?: () => void }) {
  if (!error) return null;
  const message = error instanceof Error ? error.message : "Došlo je do neočekivane greške. Pokušaj ponovo.";
  const conflict = error instanceof ApiError && error.isConcurrencyConflict;
  return (
    <div role="alert" className="flex flex-wrap items-center gap-3 rounded bg-red-50 p-3 text-sm text-red-800">
      <span className="flex-1">{message}</span>
      {conflict && (
        <button
          type="button"
          onClick={onRefresh ?? (() => window.location.reload())}
          className="min-h-10 rounded border border-red-300 bg-white px-3 font-medium text-red-800"
        >
          Osvježi
        </button>
      )}
    </div>
  );
}

export function WarningList({ warnings }: { warnings: string[] | undefined }) {
  if (!warnings?.length) return null;
  return (
    <ul className="space-y-1 rounded bg-amber-50 p-3 text-sm text-amber-900">
      {warnings.map((w) => (
        <li key={w}>{w}</li>
      ))}
    </ul>
  );
}
