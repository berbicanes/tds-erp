import { Link } from "react-router";
import { csvDate, csvNumber } from "../../lib/csv";
import { DataTable, type Column } from "../../components/DataTable";
import { PageHeader } from "../../components/PageHeader";
import { useListState } from "../../hooks/useListState";
import { formatDate, formatHours, formatPieces } from "../../lib/format";
import { todaySarajevo } from "../../lib/date";
import { inputClass } from "../../components/form/Field";
import { useProductionEntries, type ProductionEntry } from "./api";

const columns: Column<ProductionEntry>[] = [
  { key: "number", header: "Zapis", sortKey: "number", render: (e) => e.number },
  { key: "date", header: "Datum", sortKey: "date", render: (e) => formatDate(e.date), mobile: true, csv: (e) => csvDate(e.date) },
  { key: "wo", header: "Nalog", sortKey: "workOrderNumber", render: (e) => <span>{e.workOrderNumber}<span className="block text-xs text-slate-500">{e.customer}</span></span>, mobile: true, csv: (e) => `${e.workOrderNumber} ${e.customer}` },
  { key: "operator", header: "Operater", sortKey: "operatorName", render: (e) => e.operatorName },
  { key: "laser", header: "Laser", align: "right", render: (e) => formatPieces(e.laserCutQty), csv: (e) => csvNumber(e.laserCutQty) },
  { key: "press", header: "Presa", align: "right", render: (e) => formatPieces(e.pressedQty), csv: (e) => csvNumber(e.pressedQty) },
  { key: "good", header: "Gotovo", sortKey: "goodQty", align: "right", render: (e) => formatPieces(e.goodQty), mobile: true, csv: (e) => csvNumber(e.goodQty) },
  { key: "scrap", header: "Škart", align: "right", render: (e) => formatPieces(e.scrapQty), csv: (e) => csvNumber(e.scrapQty) },
  { key: "hours", header: "Sati", align: "right", render: (e) => formatHours(e.laborHours), csv: (e) => csvNumber(e.laborHours) },
  { key: "machines", header: "Oprema", render: (e) => e.equipmentCodes.join(", ") },
];

/** UI §2 "Dnevna proizvodnja". Date range in the URL (?from=…&to=…), e.g. from the dashboard "Danas gotovo". */
export function ProductionListPage() {
  const [state, setState] = useListState();
  const query = useProductionEntries(state);
  const setDate = (key: "from" | "to", value: string) => {
    const filters = { ...state.filters };
    if (value) filters[key] = [value];
    else delete filters[key];
    setState({ ...state, filters, page: 1 });
  };
  const today = todaySarajevo();
  const onlyToday = state.filters.from?.[0] === today && state.filters.to?.[0] === today;

  return (
    <>
      <PageHeader title="Dnevna proizvodnja">
        <Link to="/proizvodnja/novi" className="flex min-h-10 items-center rounded bg-blue-700 px-4 text-sm font-medium text-white">Novi zapis</Link>
      </PageHeader>
      <DataTable
        exportPath="/api/production-entries"
        exportName="dnevna-proizvodnja"
        columns={columns}
        query={query}
        state={state}
        onStateChange={setState}
        rowKey={(e) => e.id}
        rowHref={(e) => `/proizvodnja/${e.id}`}
        emptyText="Nema dnevnih zapisa za odabrani period."
        searchPlaceholder="Zapis, nalog, kupac, operater ili lot"
        toolbar={
          <div className="flex flex-wrap items-center gap-2 text-sm">
            <button type="button" aria-pressed={onlyToday}
              onClick={() => setState({ ...state, filters: onlyToday ? {} : { from: [today], to: [today] }, page: 1 })}
              className={`min-h-8 rounded-full border px-3 text-xs ${onlyToday ? "border-blue-700 bg-blue-700 text-white" : "border-slate-300"}`}>Danas</button>
            <label className="flex items-center gap-1">Od <input type="date" className={`${inputClass} h-9 w-auto`} value={state.filters.from?.[0] ?? ""} onChange={(e) => setDate("from", e.target.value)} /></label>
            <label className="flex items-center gap-1">Do <input type="date" className={`${inputClass} h-9 w-auto`} value={state.filters.to?.[0] ?? ""} onChange={(e) => setDate("to", e.target.value)} /></label>
          </div>
        }
      />
    </>
  );
}
