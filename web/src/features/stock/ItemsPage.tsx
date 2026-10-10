import { Link } from "react-router";
import { DataTable, type Column } from "../../components/DataTable";
import { useListState } from "../../hooks/useListState";
import { EMPTY, formatDate, formatQuantity } from "../../lib/format";
import { hasRole, useMe } from "../../lib/auth";
import { qtyWithUnit } from "../../lib/material";
import { useMaterialItems, type MaterialItem } from "./itemsApi";

const columns: Column<MaterialItem>[] = [
  { key: "name", header: "Artikal", sortKey: "name", render: (m) => m.name },
  { key: "grade", header: "Kvalitet", sortKey: "grade", render: (m) => m.grade ?? EMPTY },
  { key: "thickness", header: "Debljina mm", sortKey: "thicknessMm", align: "right", render: (m) => (m.thicknessMm == null ? EMPTY : formatQuantity(m.thicknessMm)) },
  { key: "unit", header: "Jedinica", render: (m) => m.unit },
  { key: "min", header: "Minimum", align: "right", render: (m) => qtyWithUnit(m.minimumStock, m.unit) },
  { key: "stocktake", header: "Sljedeći popis", sortKey: "nextStocktakeDate", render: (m) => formatDate(m.nextStocktakeDate) },
];

export function ItemsPage() {
  const [state, setState] = useListState();
  const query = useMaterialItems(state);
  const me = useMe();
  return (
    <DataTable
      columns={columns}
      query={query}
      state={state}
      onStateChange={setState}
      rowKey={(m) => m.id}
      rowHref={(m) => `/zalihe/artikli/${m.id}`}
      emptyText="Nema artikala."
      searchPlaceholder="Naziv ili kvalitet"
      toolbar={
        hasRole(me.data, "Admin", "Kontrolor") ? (
          <Link to="/zalihe/artikli/novi" className="ml-auto flex min-h-10 items-center rounded bg-blue-700 px-4 text-sm font-medium text-white">Novi artikal</Link>
        ) : null
      }
    />
  );
}
