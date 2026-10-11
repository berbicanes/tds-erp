import type { ReactNode } from "react";
import { Link, useParams } from "react-router";
import { Badge } from "../../components/Badge";
import { ErrorBanner } from "../../components/ErrorBanner";
import { EvidenceFiles } from "../../components/EvidenceFiles";
import { hasRole, useMe } from "../../lib/auth";
import { EMPTY, formatDate, formatPieces } from "../../lib/format";
import { calibrationStatusAtCheck, inspectionPhase, inspectionResult, labelOf } from "../../lib/labels";
import { useCheck, useCheckEvidence } from "./api";
import { NcCell, needsCalibrationWarning, VoidCheckButton } from "./CheckTable";

function Info({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className="text-sm">{children}</dd>
    </div>
  );
}

/** A check is not edited (DOMAIN §7.0); evidence can be added, Admin voids. */
export function CheckDetailPage() {
  const { id } = useParams();
  const detail = useCheck(id);
  const evidence = useCheckEvidence(id ?? "");
  const me = useMe();

  if (detail.isPending) return <p className="p-4 text-slate-600">Učitavanje…</p>;
  if (detail.isError) return <div className="p-4"><ErrorBanner error={detail.error} /></div>;
  const { check: c, evidence: files } = detail.data;
  const cal = labelOf(calibrationStatusAtCheck, c.calibrationStatusAtCheck);

  return (
    <div className="max-w-4xl space-y-4 px-4 py-4">
      <Link to="/kvalitet/kontrole" className="text-sm text-blue-800 underline">← Kontrole</Link>
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-lg font-semibold tabular-nums">Kontrola {c.number}</h2>
        {c.isVoided ? <Badge label={{ text: "Poništena", tone: "zinc" }} /> : <Badge label={labelOf(inspectionResult, c.result)} />}
        {hasRole(me.data, "Admin") && !c.isVoided && <span className="ml-auto"><VoidCheckButton check={c} /></span>}
      </div>
      {c.isVoided && <p className="rounded bg-slate-100 p-2 text-sm">Poništena: {c.voidedReason}</p>}
      {needsCalibrationWarning(c.calibrationStatusAtCheck) && (
        <p className={`rounded p-2 text-sm ${c.calibrationStatusAtCheck === "VanKalibracije" ? "bg-red-50 text-red-800" : "bg-amber-50 text-amber-900"}`}>
          Trajna oznaka: mjerno sredstvo {c.measuringEquipmentCode} je u trenutku kontrole bilo „{cal.text.toLowerCase()}”.
        </p>
      )}
      <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
        <Info label="Faza">{labelOf(inspectionPhase, c.phase).text}</Info>
        <Info label={c.materialLotId ? "Lot" : "Nalog"}>
          {c.workOrderId ? <Link to={`/nalozi/${c.workOrderId}`} className="text-blue-800 underline">{c.workOrderNumber}</Link>
            : c.materialLotId ? <Link to={`/zalihe/lotovi/${c.materialLotId}`} className="text-blue-800 underline">{c.lotNumber}</Link> : EMPTY}
        </Info>
        <Info label="Datum kontrole">{formatDate(c.inspectionDate)}</Info>
        <Info label="Kontrolisao">{c.inspectedByName}</Info>
        <div className="col-span-2"><Info label="Karakteristika / zahtjev crteža">{c.characteristic}</Info></div>
        <Info label="Kriterij prihvatanja">{c.acceptanceCriterion ?? EMPTY}</Info>
        <Info label="Izmjereni rezultat">{c.measuredResult ?? EMPTY}</Info>
        <Info label="Veličina uzorka">{c.sampleSize == null ? EMPTY : formatPieces(c.sampleSize)}</Info>
        <Info label="Mjerno sredstvo">
          {c.measuringEquipmentId ? <Link to={`/oprema/${c.measuringEquipmentId}`} className="text-blue-800 underline">{c.measuringEquipmentCode} {c.measuringEquipmentName}</Link> : EMPTY}
        </Info>
        <Info label="Kalibracija pri kontroli"><Badge label={cal} /></Info>
        <Info label="Neusaglašenost"><NcCell check={c} /></Info>
      </dl>
      <EvidenceFiles title="Dokazi / zapisi" files={files} add={evidence.add} remove={evidence.remove}
        canAdd={hasRole(me.data, "Admin", "Kontrolor")} canRemove={hasRole(me.data, "Admin")} />
    </div>
  );
}
