import { Link } from "react-router";
import { ErrorBanner } from "../../components/ErrorBanner";
import { formatDate, formatHours, formatPieces, formatQuantity } from "../../lib/format";
import type { WorkOrder } from "../workOrders/api";
import { useWorkOrderProduction } from "./api";

/** UI §6 "Proizvodnja": entries of the order and totals (voided entries shown struck through, not counted). */
export function WorkOrderProductionTab({ wo }: { wo: WorkOrder }) {
  const data = useWorkOrderProduction(wo.id);
  const t = data.data?.totals;
  return (
    <div className="space-y-3 pt-3">
      <Link to={`/proizvodnja/novi?nalog=${wo.id}`} className="inline-flex min-h-11 items-center rounded bg-blue-700 px-4 text-sm font-medium text-white">Novi dnevni zapis</Link>
      <ErrorBanner error={data.error} />
      <div className="overflow-x-auto rounded border border-slate-200">
        <table className="w-full text-sm">
          <thead className="bg-slate-50 text-left text-xs text-slate-600">
            <tr>
              <th className="px-2 py-2 font-medium">Zapis</th>
              <th className="px-2 py-2 font-medium">Datum</th>
              <th className="px-2 py-2 font-medium">Operater</th>
              <th className="px-2 py-2 text-right font-medium">Laser</th>
              <th className="px-2 py-2 text-right font-medium">Presa</th>
              <th className="px-2 py-2 text-right font-medium">Gotovo</th>
              <th className="px-2 py-2 text-right font-medium">Škart</th>
              <th className="px-2 py-2 text-right font-medium">Sati</th>
              <th className="px-2 py-2 font-medium">Lot / utrošak</th>
            </tr>
          </thead>
          <tbody>
            {data.data?.items.length === 0 && <tr><td colSpan={9} className="px-2 py-4 text-center text-slate-600">Na nalogu još nema dnevnih zapisa.</td></tr>}
            {data.data?.items.map((e) => (
              <tr key={e.id} className={`border-t border-slate-100 ${e.isVoided ? "text-slate-400 line-through" : ""}`}>
                <td className="px-2 py-1.5"><Link to={`/proizvodnja/${e.id}`} className="text-blue-800 hover:underline">{e.number}</Link></td>
                <td className="px-2 py-1.5">{formatDate(e.date)}</td>
                <td className="px-2 py-1.5">{e.operatorName}</td>
                <td className="px-2 py-1.5 text-right tabular-nums">{formatPieces(e.laserCutQty)}</td>
                <td className="px-2 py-1.5 text-right tabular-nums">{formatPieces(e.pressedQty)}</td>
                <td className="px-2 py-1.5 text-right tabular-nums">{formatPieces(e.goodQty)}</td>
                <td className="px-2 py-1.5 text-right tabular-nums">{formatPieces(e.scrapQty)}</td>
                <td className="px-2 py-1.5 text-right tabular-nums">{formatHours(e.laborHours)}</td>
                <td className="px-2 py-1.5">{e.lotNumber ?? ""}{e.materialConsumed != null ? ` · ${formatQuantity(e.materialConsumed)} ${e.lotUnit ?? ""}` : ""}</td>
              </tr>
            ))}
          </tbody>
          {t && t.entries > 0 && (
            <tfoot className="border-t-2 border-slate-300 font-semibold">
              <tr>
                <td className="px-2 py-1.5" colSpan={3}>Ukupno ({t.entries} zapisa)</td>
                <td className="px-2 py-1.5 text-right tabular-nums">{formatPieces(t.laserCutQty)}</td>
                <td className="px-2 py-1.5 text-right tabular-nums">{formatPieces(t.pressedQty)}</td>
                <td className="px-2 py-1.5 text-right tabular-nums">{formatPieces(t.goodQty)}</td>
                <td className="px-2 py-1.5 text-right tabular-nums">{formatPieces(t.scrapQty)}</td>
                <td className="px-2 py-1.5 text-right tabular-nums">{formatHours(t.laborHours)}</td>
                <td />
              </tr>
            </tfoot>
          )}
        </table>
      </div>
    </div>
  );
}
