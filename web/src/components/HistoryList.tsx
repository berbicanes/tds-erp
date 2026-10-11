import { ErrorBanner } from "./ErrorBanner";
import { actionText, historyLines } from "../lib/history";
import { useHistory, type HistoryScope } from "../lib/historyApi";
import { formatDateTime } from "../lib/format";
import { userLabel } from "../lib/labels";

/**
 * UI §6 Istorija: "12.10.2026. 14:32 — Anes: Status Laser → Presanje", for the record and the records that
 * belong to it, newest first, with override and void reasons. Never raw JSON.
 */
export function HistoryList({ scope, id }: { scope: HistoryScope; id: string }) {
  const history = useHistory(scope, id);
  if (history.isPending) return <p className="py-3 text-sm text-slate-600">Učitavanje…</p>;
  if (history.isError) return <ErrorBanner error={history.error} />;
  if (history.data.length === 0) return <p className="py-3 text-sm text-slate-600">Još nema zapisa u istoriji.</p>;

  return (
    <ol className="space-y-3 py-3">
      {history.data.map((e) => {
        const lines = historyLines(e);
        return (
          <li key={e.id} className={`rounded border px-3 py-2 text-sm ${e.action === "Void" ? "border-red-200 bg-red-50" : "border-slate-200"}`}>
            <p className="flex flex-wrap gap-x-2 text-slate-700">
              <span className="tabular-nums">{formatDateTime(e.at)}</span>
              <span>— {userLabel(e.userName)}</span>
              <span className="font-medium text-slate-900">{e.recordLabel}</span>
              <span className={e.action === "Void" ? "font-semibold text-red-800" : "text-slate-600"}>{actionText[e.action]}</span>
            </p>
            {e.reason && <p className="mt-1"><span className="font-medium">Razlog:</span> {e.reason}</p>}
            {lines.length > 0 && (
              <ul className="mt-1 space-y-0.5 text-slate-800">
                {lines.map((l, i) => (
                  <li key={i} className="break-words">
                    <span className="text-slate-600">{l.label}:</span>{" "}
                    {e.action === "Create" ? l.new : <>{l.old} → {l.new}</>}
                  </li>
                ))}
              </ul>
            )}
          </li>
        );
      })}
    </ol>
  );
}
