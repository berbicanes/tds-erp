import { Link } from "react-router";
import { DataTable, type Column } from "../../components/DataTable";
import { useListState } from "../../hooks/useListState";
import { toggleFilter } from "../../lib/listParams";
import { EMPTY } from "../../lib/format";
import { useEmployees, type Employee } from "./api";

const columns: Column<Employee>[] = [
  { key: "fullName", header: "Ime i prezime", sortKey: "fullName", render: (e) => e.fullName },
  { key: "position", header: "Radno mjesto", sortKey: "position", render: (e) => e.position ?? EMPTY },
  { key: "user", header: "Korisnički nalog", render: (e) => e.userName ?? "Nema" },
  { key: "isActive", header: "Status", sortKey: "isActive", render: (e) => (e.isActive ? "Aktivan" : "Neaktivan") },
];

export function EmployeesPage() {
  const [state, setState] = useListState();
  const query = useEmployees(state);
  const active = state.filters.isActive?.[0];

  const chip = (value: "true" | "false", text: string) => (
    <button
      type="button"
      aria-pressed={active === value}
      onClick={() => setState(toggleFilter({ ...state, filters: value === active ? state.filters : {} }, "isActive", value))}
      className={`min-h-8 rounded-full border px-3 text-xs ${active === value ? "border-blue-700 bg-blue-700 text-white" : "border-slate-300 text-slate-700"}`}
    >
      {text}
    </button>
  );

  return (
    <DataTable
      exportPath="/api/employees"
      exportName="zaposlenici"
      columns={columns}
      query={query}
      state={state}
      onStateChange={setState}
      rowKey={(e) => e.id}
      rowHref={(e) => `/podesavanja/zaposlenici/${e.id}`}
      emptyText="Nema zaposlenika. Dodaj prvog."
      searchPlaceholder="Ime ili radno mjesto"
      toolbar={
        <>
          {chip("true", "Aktivni")}
          {chip("false", "Neaktivni")}
          <Link to="/podesavanja/zaposlenici/novi" className="ml-auto flex min-h-10 items-center rounded bg-blue-700 px-4 text-sm font-medium text-white">
            Novi zaposlenik
          </Link>
        </>
      }
    />
  );
}
