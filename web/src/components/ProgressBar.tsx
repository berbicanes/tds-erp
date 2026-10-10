import { formatPieces } from "../lib/format";

/** Napredak = CompletedQty / OrderedQty (UI §3), never editable. */
export function ProgressBar({ completed, ordered, percent }: { completed: number; ordered: number; percent: number }) {
  return (
    <div className="flex min-w-28 items-center gap-2" title={`${percent} %`}>
      <div className="h-2 flex-1 overflow-hidden rounded bg-slate-200" role="progressbar" aria-valuenow={percent} aria-valuemin={0} aria-valuemax={100}>
        <div className={`h-full ${percent >= 100 ? "bg-green-600" : "bg-blue-600"}`} style={{ width: `${percent}%` }} />
      </div>
      <span className="whitespace-nowrap text-xs tabular-nums text-slate-600">{formatPieces(completed)}/{formatPieces(ordered)}</span>
    </div>
  );
}

/** First piece laser / press / final inspection as ✓ or – (UI §3), with text for screen readers. */
export function QualityMarks({ laser, press, final }: { laser: boolean; press: boolean; final: boolean }) {
  const mark = (ok: boolean, label: string) => (
    <span title={`${label}: ${ok ? "potvrđeno" : "nije potvrđeno"}`} className={ok ? "text-green-700" : "text-slate-400"}>
      {ok ? "✓" : "–"}
      <span className="sr-only">{`${label}: ${ok ? "potvrđeno" : "nije potvrđeno"}`}</span>
    </span>
  );
  return (
    <span className="inline-flex gap-2 font-semibold">
      {mark(laser, "Prvi komad laser")}
      {mark(press, "Prvi komad presa")}
      {mark(final, "Završna kontrola")}
    </span>
  );
}
