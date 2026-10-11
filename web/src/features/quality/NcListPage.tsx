import { Link } from "react-router";
import { DataTable, type Column } from "../../components/DataTable";
import { FilterChips } from "../../components/FilterChips";
import { Badge } from "../../components/Badge";
import { useListState } from "../../hooks/useListState";
import { hasRole, useMe } from "../../lib/auth";
import { EMPTY } from "../../lib/format";
import { labelOf, ncSource, ncStatus, productDecision } from "../../lib/labels";
import { useNcs, type NonConformity } from "./api";
import { NcDue } from "./NcTable";

const columns: Column<NonConformity>[] = [
  { key: "number", header: "NC broj", sortKey: "number", render: (n) => <span className="tabular-nums">{n.number}</span> },
  { key: "status", header: "Status", sortKey: "status", render: (n) => (n.isVoided ? `Poništena: ${n.voidedReason ?? ""}` : <Badge label={labelOf(ncStatus, n.status)} />) },
  { key: "source", header: "Izvor", sortKey: "source", render: (n) => labelOf(ncSource, n.source).text },
  { key: "desc", header: "Opis", render: (n) => n.description },
  { key: "ref", header: "Nalog / lot", render: (n) => [n.workOrderNumber, n.lotNumber ? `Lot ${n.lotNumber}` : null].filter(Boolean).join(" · ") || EMPTY },
  { key: "decision", header: "Odluka", render: (n) => labelOf(productDecision, n.productDecision).text },
  { key: "owner", header: "Vlasnik radnje", render: (n) => n.actionOwnerName ?? EMPTY },
  { key: "due", header: "Rok", sortKey: "dueDate", render: (n) => <NcDue nc={n} /> },
];

const overdue = { true: { text: "Kasni rok", tone: "red" as const } };
const voided = { true: { text: "Prikaži i poništene", tone: "slate" as const } };

export function NcListPage() {
  const [state, setState] = useListState();
  const query = useNcs(state);
  const me = useMe();
  return (
    <>
      {hasRole(me.data, "Admin", "Kontrolor") && (
        <div className="flex justify-end px-4 pt-3">
          <Link to="/kvalitet/nc/nova" className="flex min-h-10 items-center rounded bg-blue-700 px-4 text-sm font-medium text-white">Nova NC</Link>
        </div>
      )}
      <DataTable
        columns={columns}
        query={query}
        state={state}
        onStateChange={setState}
        rowKey={(n) => n.id}
        rowHref={(n) => `/kvalitet/nc/${n.id}`}
        emptyText="Nema neusaglašenosti."
        searchPlaceholder="NC broj, opis, nalog, lot, dobavljač"
        toolbar={
          <>
            <FilterChips name="status" options={ncStatus} state={state} onStateChange={setState} />
            <FilterChips name="source" options={ncSource} state={state} onStateChange={setState} />
            <FilterChips name="overdue" options={overdue} state={state} onStateChange={setState} />
            <FilterChips name="includeVoided" options={voided} state={state} onStateChange={setState} />
          </>
        }
      />
    </>
  );
}
