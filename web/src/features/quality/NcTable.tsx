import { Link } from "react-router";
import { Badge } from "../../components/Badge";
import { EMPTY, formatDate } from "../../lib/format";
import { labelOf, ncSource, ncStatus, productDecision } from "../../lib/labels";
import { todaySarajevo } from "../../lib/date";
import type { NonConformity } from "./api";

export function NcDue({ nc }: { nc: Pick<NonConformity, "dueDate" | "status"> }) {
  if (!nc.dueDate) return <>{EMPTY}</>;
  const late = nc.status !== "Zatvorena" && nc.dueDate < todaySarajevo();
  return <span className={late ? "font-semibold text-red-700" : ""}>{formatDate(nc.dueDate)}{late ? " · kasni" : ""}</span>;
}

export function NcTable({ ncs, emptyText }: { ncs: NonConformity[] | undefined; emptyText: string }) {
  return (
    <div className="overflow-x-auto rounded border border-slate-200">
      <table className="w-full text-sm cards-sm">
        <thead className="bg-slate-50 text-left text-xs text-slate-600">
          <tr>
            <th className="px-2 py-2 font-medium">NC broj</th>
            <th className="px-2 py-2 font-medium">Izvor</th>
            <th className="px-2 py-2 font-medium">Opis</th>
            <th className="px-2 py-2 font-medium">Odluka</th>
            <th className="px-2 py-2 font-medium">Status</th>
            <th className="px-2 py-2 font-medium">Rok</th>
          </tr>
        </thead>
        <tbody>
          {ncs?.length === 0 && <tr><td colSpan={6} className="px-2 py-4 text-center text-slate-600">{emptyText}</td></tr>}
          {ncs?.map((n) => (
            <tr key={n.id} className={`border-t border-slate-100 ${n.isVoided ? "text-slate-400" : ""}`}>
              <td data-label="NC broj" className="px-2 py-1.5 tabular-nums">
                <Link to={`/kvalitet/nc/${n.id}`} className="text-blue-800 underline">{n.number}</Link>
                {n.isVoided && <span className="block text-xs">Poništena: {n.voidedReason}</span>}
              </td>
              <td data-label="Izvor" className="px-2 py-1.5">{labelOf(ncSource, n.source).text}</td>
              <td data-label="Opis" className="px-2 py-1.5">{n.description}</td>
              <td data-label="Odluka" className="px-2 py-1.5">{labelOf(productDecision, n.productDecision).text}</td>
              <td data-label="Status" className="px-2 py-1.5">{n.isVoided ? "Poništena" : <Badge label={labelOf(ncStatus, n.status)} />}</td>
              <td data-label="Rok" className="px-2 py-1.5"><NcDue nc={n} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}
