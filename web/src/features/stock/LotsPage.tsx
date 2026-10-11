import { Link } from "react-router";
import { csvDate } from "../../lib/csv";
import { DataTable, type Column } from "../../components/DataTable";
import { FilterChips } from "../../components/FilterChips";
import { Badge } from "../../components/Badge";
import { useListState } from "../../hooks/useListState";
import { labelOf, receiptDecision } from "../../lib/labels";
import { EMPTY, formatDate } from "../../lib/format";
import { hasRole, useMe } from "../../lib/auth";
import { itemTitle, qtyWithUnit } from "../../lib/material";
import { useLots, type Lot } from "./lotsApi";

const columns: Column<Lot>[] = [
  { key: "lotNumber", header: "Lot / heat broj", sortKey: "lotNumber", render: (l) => l.lotNumber },
  { key: "item", header: "Artikal", sortKey: "itemName", render: (l) => itemTitle({ name: l.itemName, grade: l.itemGrade, thicknessMm: l.itemThicknessMm }), mobile: true },
  { key: "supplier", header: "Dobavljač", sortKey: "supplierName", render: (l) => l.supplierName },
  { key: "received", header: "Prijem", sortKey: "receivedDate", render: (l) => formatDate(l.receivedDate), csv: (l) => csvDate(l.receivedDate) },
  { key: "receivedQty", header: "Primljeno", align: "right", render: (l) => qtyWithUnit(l.receivedQty, l.unit) },
  { key: "onHand", header: "Na stanju", sortKey: "onHand", align: "right", render: (l) => qtyWithUnit(l.onHand, l.unit), mobile: true },
  { key: "decision", header: "Odluka prijema", sortKey: "receiptDecision", render: (l) => <Badge label={labelOf(receiptDecision, l.receiptDecision)} />, mobile: true },
  { key: "location", header: "Lokacija", render: (l) => l.storageLocation ?? EMPTY },
];

export function LotsPage() {
  const [state, setState] = useListState();
  const query = useLots(state);
  const me = useMe();
  return (
    <DataTable
      exportPath="/api/lots"
      exportName="lotovi"
      columns={columns}
      query={query}
      state={state}
      onStateChange={setState}
      rowKey={(l) => l.id}
      rowHref={(l) => `/zalihe/lotovi/${l.id}`}
      emptyText="Nema lotova. Unesi prvi prijem."
      searchPlaceholder="Lot, artikal, kvalitet ili dobavljač"
      toolbar={
        <>
          <FilterChips name="receiptDecision" options={receiptDecision} state={state} onStateChange={setState} />
          {hasRole(me.data, "Admin", "Kontrolor") && (
            <Link to="/zalihe/lotovi/prijem" className="ml-auto flex min-h-10 items-center rounded bg-blue-700 px-4 text-sm font-medium text-white">Novi prijem</Link>
          )}
        </>
      }
    />
  );
}
