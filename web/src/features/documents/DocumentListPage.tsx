import { Link } from "react-router";
import { csvDate } from "../../lib/csv";
import { DataTable, type Column } from "../../components/DataTable";
import { FilterChips } from "../../components/FilterChips";
import { PageHeader } from "../../components/PageHeader";
import { Badge } from "../../components/Badge";
import { useListState } from "../../hooks/useListState";
import { hasRole, useMe } from "../../lib/auth";
import { todaySarajevo } from "../../lib/date";
import { EMPTY, formatDate } from "../../lib/format";
import { documentKind, documentStatus, labelOf } from "../../lib/labels";
import { useDocuments, type ControlledDocument } from "./api";

function Review({ d }: { d: ControlledDocument }) {
  if (!d.nextReviewDate) return <>{EMPTY}</>;
  const late = d.status === "Odobren" && d.nextReviewDate < todaySarajevo();
  return <span className={late ? "font-semibold text-red-700" : ""}>{formatDate(d.nextReviewDate)}{late ? " · kasni" : ""}</span>;
}

const columns: Column<ControlledDocument>[] = [
  { key: "code", header: "Oznaka", sortKey: "code", render: (d) => d.code },
  { key: "version", header: "Verzija", render: (d) => d.docVersion },
  { key: "title", header: "Naziv", sortKey: "title", render: (d) => d.title, mobile: true },
  { key: "kind", header: "Vrsta", render: (d) => labelOf(documentKind, d.kind).text },
  { key: "status", header: "Status", sortKey: "status", render: (d) => (d.isVoided ? `Poništen: ${d.voidedReason ?? ""}` : <Badge label={labelOf(documentStatus, d.status)} />), mobile: true },
  { key: "owner", header: "Vlasnik", render: (d) => d.ownerName ?? EMPTY },
  { key: "approved", header: "Odobren", render: (d) => formatDate(d.approvalDate), csv: (d) => csvDate(d.approvalDate) },
  { key: "review", header: "Sljedeća provjera", sortKey: "nextReviewDate", render: (d) => <Review d={d} />, mobile: true, csv: (d) => csvDate(d.nextReviewDate) },
];

const voided = { true: { text: "Prikaži i poništene", tone: "slate" as const } };

export function DocumentListPage() {
  const [state, setState] = useListState();
  const query = useDocuments(state);
  const me = useMe();
  return (
    <>
      <PageHeader title="Dokumenti">
        {hasRole(me.data, "Admin", "Kontrolor") && (
          <Link to="/dokumenti/novi" className="flex min-h-10 items-center rounded bg-blue-700 px-4 text-sm font-medium text-white">Novi dokument</Link>
        )}
      </PageHeader>
      <DataTable
        exportPath="/api/documents"
        exportName="dokumenti"
        columns={columns}
        query={query}
        state={state}
        onStateChange={setState}
        rowKey={(d) => d.id}
        rowHref={(d) => `/dokumenti/${d.id}`}
        emptyText="Nema dokumenata."
        searchPlaceholder="Oznaka ili naziv"
        toolbar={
          <>
            <FilterChips name="status" options={documentStatus} state={state} onStateChange={setState} />
            <FilterChips name="kind" options={documentKind} state={state} onStateChange={setState} />
            <FilterChips name="includeVoided" options={voided} state={state} onStateChange={setState} />
          </>
        }
      />
    </>
  );
}
