import { Link } from "react-router";
import { csvDate } from "../../lib/csv";
import { DataTable, type Column } from "../../components/DataTable";
import { FilterChips } from "../../components/FilterChips";
import { PageHeader } from "../../components/PageHeader";
import { Badge } from "../../components/Badge";
import { useListState } from "../../hooks/useListState";
import { calibrationStatusAtCheck, equipmentKind, labelOf } from "../../lib/labels";
import { EMPTY, formatDate } from "../../lib/format";
import { hasRole, useMe } from "../../lib/auth";
import { useEquipmentList, type Equipment } from "./api";

const columns: Column<Equipment>[] = [
  { key: "code", header: "Oznaka", sortKey: "code", render: (e) => e.code },
  { key: "name", header: "Naziv", sortKey: "name", render: (e) => e.name, mobile: true },
  { key: "kind", header: "Vrsta", sortKey: "kind", render: (e) => <Badge label={labelOf(equipmentKind, e.kind)} /> },
  { key: "manufacturer", header: "Proizvođač", render: (e) => e.manufacturer ?? EMPTY },
  { key: "next", header: "Sljedeća kalibracija", sortKey: "nextCalibrationDate", render: (e) => (e.kind === "MjernoSredstvo" ? formatDate(e.nextCalibrationDate) : ""), mobile: true, csv: (e) => csvDate(e.nextCalibrationDate) },
  { key: "cal", header: "Kalibracija", render: (e) => (e.calibrationStatus ? <Badge label={labelOf(calibrationStatusAtCheck, e.calibrationStatus)} /> : ""), mobile: true },
  { key: "active", header: "Status", sortKey: "isActive", render: (e) => (e.isActive ? "Aktivna" : "Neaktivna") },
];

export function EquipmentListPage() {
  const [state, setState] = useListState();
  const query = useEquipmentList(state);
  const me = useMe();
  return (
    <>
      <PageHeader title="Oprema">
        {hasRole(me.data, "Admin", "Kontrolor") && (
          <Link to="/oprema/nova" className="flex min-h-10 items-center rounded bg-blue-700 px-4 text-sm font-medium text-white">Nova oprema</Link>
        )}
      </PageHeader>
      <DataTable
        exportPath="/api/equipment"
        exportName="oprema"
        columns={columns}
        query={query}
        state={state}
        onStateChange={setState}
        rowKey={(e) => e.id}
        rowHref={(e) => `/oprema/${e.id}`}
        emptyText="Nema opreme."
        searchPlaceholder="Oznaka, naziv ili serijski broj"
        toolbar={
          <>
            <FilterChips name="kind" options={equipmentKind} state={state} onStateChange={setState} />
            <FilterChips name="calibrationStatus"
              options={{ VanKalibracije: calibrationStatusAtCheck.VanKalibracije, BezDatumaKalibracije: calibrationStatusAtCheck.BezDatumaKalibracije }}
              state={state} onStateChange={setState} />
          </>
        }
      />
    </>
  );
}
