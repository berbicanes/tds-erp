import { Link } from "react-router";
import { csvNumber } from "../../lib/csv";
import { DataTable, type Column } from "../../components/DataTable";
import { FilterChips } from "../../components/FilterChips";
import { useListState } from "../../hooks/useListState";
import { EMPTY, formatQuantity } from "../../lib/format";
import { hasRole, useMe } from "../../lib/auth";
import { stockLevel } from "../../lib/labels";
import { qtyWithUnit } from "../../lib/material";
import { LevelWithIncomplete, levelText } from "../../components/LevelWithIncomplete";
import { useItemStock, type ItemStock } from "./stockApi";

const columns: Column<ItemStock>[] = [
  { key: "name", header: "Artikal", sortKey: "name", render: (m) => m.name },
  { key: "grade", header: "Kvalitet", render: (m) => m.grade ?? EMPTY },
  { key: "thickness", header: "Debljina mm", align: "right", render: (m) => (m.thicknessMm == null ? EMPTY : formatQuantity(m.thicknessMm)), csv: (m) => csvNumber(m.thicknessMm) },
  { key: "onHand", header: "Na stanju", sortKey: "onHand", align: "right", render: (m) => qtyWithUnit(m.onHand, m.unit), mobile: true },
  { key: "reserved", header: "Rezervisano", align: "right", render: (m) => qtyWithUnit(m.reserved, m.unit) },
  {
    key: "available", header: "Raspoloživo", sortKey: "available", align: "right", mobile: true,
    render: (m) => <span className={m.available < 0 ? "font-semibold text-red-700" : ""}>{qtyWithUnit(m.available, m.unit)}</span>,
  },
  { key: "min", header: "Minimum", align: "right", render: (m) => qtyWithUnit(m.minimumStock, m.unit) },
  { key: "level", header: "Nivo", render: (m) => <LevelWithIncomplete item={m} />, mobile: true, csv: (m) => levelText(m) },
  { key: "onHold", header: "Na kontroli / karantin", align: "right", render: (m) => (m.onHoldQty === 0 ? EMPTY : qtyWithUnit(m.onHoldQty, m.unit)) },
];

/** Items with stock (DOMAIN §2.4): accepted lots only; "Na kontroli / karantin" shown separately. */
export function ItemsPage() {
  const [state, setState] = useListState();
  const query = useItemStock(state);
  const me = useMe();
  return (
    <DataTable
      exportPath="/api/stock/items"
      exportName="artikli"
      columns={columns}
      query={query}
      state={state}
      onStateChange={setState}
      rowKey={(m) => m.materialItemId}
      rowHref={(m) => `/zalihe/artikli/${m.materialItemId}`}
      emptyText="Nema artikala."
      searchPlaceholder="Naziv ili kvalitet"
      toolbar={
        <>
          <FilterChips name="stockLevel" options={stockLevel} state={state} onStateChange={setState} />
          {state.filters.hasMinimum?.[0] === "true" && (
            <button type="button" aria-pressed onClick={() => { const filters = { ...state.filters }; delete filters.hasMinimum; setState({ ...state, filters, page: 1 }); }}
              className="min-h-8 rounded-full border border-blue-700 bg-blue-700 px-3 text-xs text-white">Samo sa minimumom ✕</button>
          )}
          {hasRole(me.data, "Admin", "Kontrolor") && (
            <Link to="/zalihe/artikli/novi" className="ml-auto flex min-h-10 items-center rounded bg-blue-700 px-4 text-sm font-medium text-white">Novi artikal</Link>
          )}
        </>
      }
    />
  );
}
