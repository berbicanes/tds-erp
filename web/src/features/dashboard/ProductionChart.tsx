import { useState } from "react";
import { Bar, CartesianGrid, ComposedChart, Legend, Line, ResponsiveContainer, Tooltip, XAxis, YAxis } from "recharts";
import { formatDate, formatPieces } from "../../lib/format";
import type { ChartDay } from "./api";

/**
 * Last 14 working days (UI §3): laser / presa / gotovo as grouped bars, škart as a line — one axis (pieces).
 * Categorical colours in fixed order, validated for colour blindness (dataviz validate_palette: all checks pass;
 * contrast warning on aqua/yellow is relieved by the legend, tooltips and the table view below).
 */
const SERIES = [
  { key: "laserCutQty", name: "Laser", color: "#2a78d6" },
  { key: "pressedQty", name: "Presa", color: "#eb6834" },
  { key: "goodQty", name: "Gotovo", color: "#1baf7a" },
] as const;
const SCRAP = { key: "scrapQty", name: "Škart", color: "#eda100" } as const;

const shortDate = (iso: string) => formatDate(iso).slice(0, 6); // "12.10."

export function ProductionChart({ days }: { days: ChartDay[] }) {
  const [asTable, setAsTable] = useState(false);
  return (
    <div className="space-y-2">
      <div className="flex items-center justify-between">
        <h3 className="text-sm font-semibold">Zadnjih 14 radnih dana</h3>
        <button type="button" onClick={() => setAsTable((t) => !t)} className="min-h-9 rounded border border-slate-300 px-3 text-xs">
          {asTable ? "Prikaži graf" : "Prikaži tabelu"}
        </button>
      </div>
      {asTable ? (
        <div className="overflow-x-auto rounded border border-slate-200">
          <table className="w-full text-sm">
            <thead className="bg-slate-50 text-left text-xs text-slate-600">
              <tr>
                <th className="px-2 py-1.5 font-medium">Datum</th>
                {[...SERIES, SCRAP].map((s) => <th key={s.key} className="px-2 py-1.5 text-right font-medium">{s.name}</th>)}
              </tr>
            </thead>
            <tbody>
              {days.map((d) => (
                <tr key={d.date} className="border-t border-slate-100">
                  <td className="px-2 py-1">{formatDate(d.date)}</td>
                  {[...SERIES, SCRAP].map((s) => <td key={s.key} className="px-2 py-1 text-right tabular-nums">{formatPieces(d[s.key])}</td>)}
                </tr>
              ))}
            </tbody>
          </table>
        </div>
      ) : (
        <div className="h-64 w-full" role="img" aria-label="Graf proizvodnje za zadnjih 14 radnih dana: laser, presa, gotovo i škart po danu">
          <ResponsiveContainer width="100%" height="100%">
            <ComposedChart data={days} margin={{ top: 8, right: 8, bottom: 0, left: -12 }} barGap={2} barCategoryGap="20%">
              <CartesianGrid vertical={false} stroke="#e2e8f0" />
              <XAxis dataKey="date" tickFormatter={shortDate} tick={{ fontSize: 11, fill: "#475569" }} tickLine={false} axisLine={{ stroke: "#cbd5e1" }} interval="preserveStartEnd" />
              <YAxis allowDecimals={false} tick={{ fontSize: 11, fill: "#475569" }} tickLine={false} axisLine={false} tickFormatter={(v: number) => formatPieces(v)} />
              <Tooltip
                labelFormatter={(label) => formatDate(String(label))}
                formatter={(value, name) => [formatPieces(Number(value)), name]}
                contentStyle={{ fontSize: 12, borderRadius: 4, borderColor: "#e2e8f0" }}
                cursor={{ fill: "#f1f5f9" }}
              />
              {/* Fixed series order (Laser, Presa, Gotovo, Škart), not alphabetical. */}
              <Legend itemSorter={null} wrapperStyle={{ fontSize: 12, color: "#334155" }} />
              {SERIES.map((s) => (
                <Bar key={s.key} dataKey={s.key} name={s.name} fill={s.color} radius={[4, 4, 0, 0]} maxBarSize={18} isAnimationActive={false} />
              ))}
              <Line dataKey={SCRAP.key} name={SCRAP.name} stroke={SCRAP.color} strokeWidth={2} dot={{ r: 4, fill: SCRAP.color, stroke: "#fff", strokeWidth: 2 }} isAnimationActive={false} />
            </ComposedChart>
          </ResponsiveContainer>
        </div>
      )}
    </div>
  );
}
