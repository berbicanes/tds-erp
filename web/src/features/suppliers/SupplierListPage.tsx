import { Link } from "react-router";
import { csvDate } from "../../lib/csv";
import { DataTable, type Column } from "../../components/DataTable";
import { FilterChips } from "../../components/FilterChips";
import { PageHeader } from "../../components/PageHeader";
import { Badge } from "../../components/Badge";
import { useListState } from "../../hooks/useListState";
import { labelOf, supplierApprovalStatus } from "../../lib/labels";
import { EMPTY, formatDate } from "../../lib/format";
import { hasRole, useMe } from "../../lib/auth";
import { useSuppliers, type Supplier } from "./api";

const columns: Column<Supplier>[] = [
  { key: "name", header: "Naziv", sortKey: "name", render: (s) => s.name },
  { key: "material", header: "Materijal / usluga", render: (s) => s.materialOrService ?? EMPTY },
  { key: "status", header: "Status odobrenja", sortKey: "approvalStatus", render: (s) => <Badge label={labelOf(supplierApprovalStatus, s.approvalStatus)} /> },
  { key: "next", header: "Sljedeća ocjena", sortKey: "nextEvaluationDate", render: (s) => formatDate(s.nextEvaluationDate), csv: (s) => csvDate(s.nextEvaluationDate) },
  { key: "owner", header: "Odgovorna osoba", render: (s) => s.responsibleEmployeeName ?? EMPTY },
];

export function SupplierListPage() {
  const [state, setState] = useListState();
  const query = useSuppliers(state);
  const me = useMe();
  return (
    <>
      <PageHeader title="Dobavljači">
        {hasRole(me.data, "Admin", "Kontrolor") && (
          <Link to="/dobavljaci/novi" className="flex min-h-10 items-center rounded bg-blue-700 px-4 text-sm font-medium text-white">Novi dobavljač</Link>
        )}
      </PageHeader>
      <DataTable
        exportPath="/api/suppliers"
        exportName="dobavljaci"
        columns={columns}
        query={query}
        state={state}
        onStateChange={setState}
        rowKey={(s) => s.id}
        rowHref={(s) => `/dobavljaci/${s.id}`}
        emptyText="Nema dobavljača."
        searchPlaceholder="Naziv ili materijal"
        toolbar={<FilterChips name="approvalStatus" options={supplierApprovalStatus} state={state} onStateChange={setState} />}
      />
    </>
  );
}
