import { DataTable, type Column } from "../../components/DataTable";
import { csvDate } from "../../lib/csv";
import { FilterChips } from "../../components/FilterChips";
import { Badge } from "../../components/Badge";
import { useListState } from "../../hooks/useListState";
import { EMPTY, formatDate } from "../../lib/format";
import { calibrationStatusAtCheck, inspectionPhase, inspectionResult, labelOf } from "../../lib/labels";
import { useChecks, type QualityCheck } from "./api";
import { CalibrationMark, NcCell } from "./CheckTable";

const columns: Column<QualityCheck>[] = [
  { key: "number", header: "Broj", sortKey: "number", render: (c) => <span className="tabular-nums">{c.number}</span> },
  { key: "date", header: "Datum", sortKey: "inspectionDate", render: (c) => formatDate(c.inspectionDate), mobile: true, csv: (c) => csvDate(c.inspectionDate) },
  { key: "phase", header: "Faza", sortKey: "phase", render: (c) => labelOf(inspectionPhase, c.phase).text },
  { key: "owner", header: "Nalog / lot", render: (c) => c.workOrderNumber ?? (c.lotNumber ? `Lot ${c.lotNumber}` : EMPTY), mobile: true },
  { key: "char", header: "Karakteristika", render: (c) => c.characteristic },
  { key: "result", header: "Rezultat", sortKey: "result", render: (c) => (c.isVoided ? `Poništena: ${c.voidedReason ?? ""}` : <Badge label={labelOf(inspectionResult, c.result)} />), mobile: true },
  { key: "instrument", header: "Mjerno sredstvo", render: (c) => <>{c.measuringEquipmentCode ?? EMPTY} <CalibrationMark status={c.calibrationStatusAtCheck} /></>,
    csv: (c) => (c.measuringEquipmentCode ? `${c.measuringEquipmentCode} (${labelOf(calibrationStatusAtCheck, c.calibrationStatusAtCheck).text})` : null) },
  { key: "nc", header: "NC", render: (c) => <NcCell check={c} />, csv: (c) => c.nonConformityNumber },
  { key: "by", header: "Kontrolisao", render: (c) => c.inspectedByName },
];

const flags = {
  true: { text: "Mjerno sredstvo bez važeće kalibracije", tone: "amber" as const },
};
const voided = { true: { text: "Prikaži i poništene", tone: "slate" as const } };

export function ChecksPage() {
  const [state, setState] = useListState();
  const query = useChecks(state);
  return (
    <DataTable
      exportPath="/api/quality-checks"
      exportName="kontrole"
      columns={columns}
      query={query}
      state={state}
      onStateChange={setState}
      rowKey={(c) => c.id}
      rowHref={(c) => `/kvalitet/kontrole/${c.id}`}
      emptyText="Nema kontrola."
      searchPlaceholder="Broj, karakteristika, nalog, lot, kontrolor"
      toolbar={
        <>
          <FilterChips name="phase" options={inspectionPhase} state={state} onStateChange={setState} />
          <FilterChips name="result" options={inspectionResult} state={state} onStateChange={setState} />
          <FilterChips name="calibrationWarning" options={flags} state={state} onStateChange={setState} />
          <FilterChips name="includeVoided" options={voided} state={state} onStateChange={setState} />
        </>
      }
    />
  );
}
