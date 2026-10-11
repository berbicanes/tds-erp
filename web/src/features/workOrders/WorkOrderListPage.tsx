import { Link } from "react-router";
import { csvDate } from "../../lib/csv";
import { DataTable, type Column } from "../../components/DataTable";
import { FilterChips } from "../../components/FilterChips";
import { PageHeader } from "../../components/PageHeader";
import { Badge } from "../../components/Badge";
import { ProgressBar, QualityMarks } from "../../components/ProgressBar";
import { useListState } from "../../hooks/useListState";
import { labelOf, priority, workOrderStatus } from "../../lib/labels";
import { formatDate } from "../../lib/format";
import { hasRole, useMe } from "../../lib/auth";
import { dueTone, isFinished } from "../../lib/workOrder";
import { useWorkOrders, type WorkOrder } from "./api";

const columns: Column<WorkOrder>[] = [
  { key: "number", header: "Broj", sortKey: "number", render: (w) => w.number },
  { key: "customer", header: "Kupac", sortKey: "customer", render: (w) => w.customer, mobile: true },
  { key: "product", header: "Proizvod", sortKey: "productDescription", render: (w) => <span className="line-clamp-2">{w.productDescription}</span> },
  { key: "status", header: "Status", sortKey: "status", render: (w) => <Badge label={labelOf(workOrderStatus, w.status)} />, mobile: true },
  { key: "priority", header: "Prioritet", sortKey: "priority", render: (w) => (w.priority === "Hitno" ? <Badge label={labelOf(priority, w.priority)} /> : "Normalan") },
  { key: "progress", header: "Napredak", render: (w) => <ProgressBar completed={w.completedQty} ordered={w.orderedQty} percent={w.progressPercent} />, csv: (w) => `${w.completedQty} / ${w.orderedQty}` },
  {
    key: "due", header: "Rok", sortKey: "dueDate", mobile: true, csv: (w) => csvDate(w.dueDate),
    render: (w) => {
      const tone = dueTone(w.dueDate, w.isOverdue, isFinished(w.status));
      return <span className={tone === "red" ? "font-semibold text-red-700" : tone === "amber" ? "font-semibold text-amber-800" : ""}>{formatDate(w.dueDate)}{w.isOverdue ? " · kasni" : ""}</span>;
    },
  },
  { key: "quality", header: "Kvalitet", render: (w) => <QualityMarks laser={w.laserFirstPieceApproved} press={w.pressFirstPieceApproved} final={w.finalInspectionPassed} />,
    csv: (w) => `Laser: ${w.laserFirstPieceApproved ? "Da" : "Ne"}, Presa: ${w.pressFirstPieceApproved ? "Da" : "Ne"}, Završna: ${w.finalInspectionPassed ? "Da" : "Ne"}` },
];

/** Boolean shortcut filters from the dashboard (UI §3: every number links to a filtered list). */
const shortcuts: { key: "active" | "overdue" | "dueWithinDays"; value: string; text: string }[] = [
  { key: "active", value: "true", text: "Aktivni" },
  { key: "overdue", value: "true", text: "Kasne" },
  { key: "dueWithinDays", value: "7", text: "Rok u 7 dana" },
];

export function WorkOrderListPage() {
  const [state, setState] = useListState();
  const query = useWorkOrders(state);
  const me = useMe();
  return (
    <>
      <PageHeader title="Nalozi">
        {hasRole(me.data, "Admin", "Kontrolor") && (
          <Link to="/nalozi/novi" className="flex min-h-10 items-center rounded bg-blue-700 px-4 text-sm font-medium text-white">Novi nalog</Link>
        )}
      </PageHeader>
      <DataTable
        exportPath="/api/work-orders"
        exportName="nalozi"
        columns={columns}
        query={query}
        state={state}
        onStateChange={setState}
        rowKey={(w) => w.id}
        rowHref={(w) => `/nalozi/${w.id}`}
        emptyText="Nema naloga."
        searchPlaceholder="Broj, kupac, proizvod ili crtež"
        toolbar={
          <div className="flex w-full flex-col gap-2">
            <div className="flex flex-wrap gap-1">
              {shortcuts.map((s) => {
                const on = state.filters[s.key]?.[0] === s.value;
                return (
                  <button key={s.key} type="button" aria-pressed={on}
                    onClick={() => {
                      const filters = { ...state.filters };
                      if (on) delete filters[s.key];
                      else filters[s.key] = [s.value];
                      setState({ ...state, filters, page: 1 });
                    }}
                    className={`min-h-8 rounded-full border px-3 text-xs ${on ? "border-blue-700 bg-blue-700 text-white" : "border-slate-300 bg-white text-slate-700"}`}>
                    {s.text}
                  </button>
                );
              })}
              <FilterChips name="priority" options={{ Hitno: priority.Hitno }} state={state} onStateChange={setState} />
            </div>
            <FilterChips name="status" options={workOrderStatus} state={state} onStateChange={setState} />
          </div>
        }
      />
    </>
  );
}
