import { useState, type ReactNode } from "react";
import { Link, useParams } from "react-router";
import { Badge } from "../../components/Badge";
import { ErrorBanner } from "../../components/ErrorBanner";
import { ProgressBar, QualityMarks } from "../../components/ProgressBar";
import { hasRole, useMe } from "../../lib/auth";
import { labelOf, priority, qualityApproval, workOrderStatus, type QualityApproval } from "../../lib/labels";
import { EMPTY, formatDate, formatPieces } from "../../lib/format";
import { fileUrl } from "../../lib/files";
import { dueTone, isFinished } from "../../lib/workOrder";
import { useSetQualityApproval, useWorkOrder, type WorkOrder } from "./api";
import { WorkOrderQualityTab } from "../quality/WorkOrderQualityTab";
import { WorkOrderMaterialsTab } from "./WorkOrderMaterialsTab";
import { StatusChange } from "./StatusChange";
import { WorkOrderProductionTab } from "../production/WorkOrderProductionTab";

function Info({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className="text-sm">{children}</dd>
    </div>
  );
}

function QualityApprovalControl({ wo }: { wo: WorkOrder }) {
  const set = useSetQualityApproval(wo.id);
  const me = useMe();
  if (!hasRole(me.data, "Admin", "Kontrolor") || ["Isporuceno", "Otkazano"].includes(wo.status))
    return <Badge label={labelOf(qualityApproval, wo.qualityApproval)} />;
  return (
    <div className="flex flex-wrap items-center gap-2">
      <select aria-label="Odobrenje kvaliteta" value={wo.qualityApproval} disabled={set.isPending}
        onChange={(e) => set.mutate({ qualityApproval: e.target.value as QualityApproval, version: wo.version })}
        className="h-9 rounded border border-slate-300 px-2 text-sm">
        {Object.entries(qualityApproval).map(([v, l]) => <option key={v} value={v}>{l.text}</option>)}
      </select>
      <ErrorBanner error={set.error} />
    </div>
  );
}

type DetailTab = "osnovno" | "proizvodnja" | "materijal" | "kvalitet";

/** UI §6. Tabs Proizvodnja, Materijal, Kvalitet are added as the modules are built; Istorija comes in Phase 3. */
export function WorkOrderDetailPage() {
  const { id } = useParams();
  const wo = useWorkOrder(id);
  const me = useMe();
  const [tab, setTab] = useState<DetailTab>("osnovno");

  if (wo.isPending) return <p className="p-4 text-slate-600">Učitavanje…</p>;
  if (wo.isError) return <div className="p-4"><ErrorBanner error={wo.error} /></div>;
  const w = wo.data;
  const tone = dueTone(w.dueDate, w.isOverdue, isFinished(w.status));

  const tabs: { key: DetailTab; label: string }[] = [
    { key: "osnovno", label: "Osnovno" },
    { key: "proizvodnja", label: "Proizvodnja" },
    { key: "materijal", label: "Materijal" },
    { key: "kvalitet", label: "Kvalitet" },
  ];

  return (
    <div className="space-y-4">
      <div className="space-y-2 border-b border-slate-200 px-4 py-3">
        <Link to="/nalozi" className="text-sm text-blue-800 underline">← Nalozi</Link>
        <div className="flex flex-wrap items-center gap-3">
          <h1 className="text-lg font-semibold tabular-nums">{w.number}</h1>
          <Badge label={labelOf(workOrderStatus, w.status)} />
          {w.priority === "Hitno" && <Badge label={labelOf(priority, w.priority)} />}
          {w.readyForShipment && <Badge label={{ text: "Spremno za isporuku", tone: "green" }} />}
          {hasRole(me.data, "Admin", "Kontrolor") && (
            <Link to={`/nalozi/${w.id}/uredi`} className="ml-auto flex min-h-10 items-center rounded border border-slate-300 px-4 text-sm">Uredi</Link>
          )}
        </div>
        <p className="text-sm text-slate-700">{w.customer} · {w.productDescription}</p>
        {hasRole(me.data, "Admin", "Kontrolor") && w.status !== "Isporuceno" && <StatusChange key={w.version} wo={w} />}
      </div>

      <nav aria-label="Detalj naloga" className="flex gap-1 overflow-x-auto border-b border-slate-200 px-4">
        {tabs.map((t) => (
          <button key={t.key} type="button" onClick={() => setTab(t.key)} aria-pressed={tab === t.key}
            className={`flex min-h-11 items-center whitespace-nowrap border-b-2 px-3 text-sm ${tab === t.key ? "border-blue-700 font-semibold text-blue-800" : "border-transparent text-slate-600"}`}>
            {t.label}
          </button>
        ))}
      </nav>

      <div className="px-4 pb-6">
        {tab === "proizvodnja" && <WorkOrderProductionTab wo={w} />}
        {tab === "materijal" && <WorkOrderMaterialsTab wo={w} />}
        {tab === "kvalitet" && <WorkOrderQualityTab wo={w} />}
        {tab === "osnovno" && (
          <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <Info label="Naručeno">{formatPieces(w.orderedQty)} kom</Info>
            <Info label="Odrađeno (gotovi ispravni)"><ProgressBar completed={w.completedQty} ordered={w.orderedQty} percent={w.progressPercent} /></Info>
            <Info label="Rok isporuke">
              <span className={tone === "red" ? "font-semibold text-red-700" : tone === "amber" ? "font-semibold text-amber-800" : ""}>
                {formatDate(w.dueDate)}{w.isOverdue ? " · kasni" : ""}
              </span>
            </Info>
            <Info label="Datum puštanja">{formatDate(w.releaseDate)}</Info>
            <Info label="Odobrenje kvaliteta"><QualityApprovalControl wo={w} /></Info>
            <Info label="Kvalitet (laser · presa · završna)"><QualityMarks laser={w.laserFirstPieceApproved} press={w.pressFirstPieceApproved} final={w.finalInspectionPassed} /></Info>
            <Info label="Odgovorna osoba">{w.responsibleEmployeeName ?? EMPTY}</Info>
            <Info label="Crtež">{[w.drawingNumber, w.drawingRevision ? `rev. ${w.drawingRevision}` : null].filter(Boolean).join(" ") || EMPTY}</Info>
            <Info label="Važeći crtež">
              {w.drawingFileId ? (
                <span className="flex flex-wrap gap-3">
                  <a href={fileUrl(w.drawingFileId)} target="_blank" rel="noreferrer" className="text-blue-800 underline">Otvori</a>
                  <a href={fileUrl(w.drawingFileId)} download={w.drawingFileName ?? undefined} className="text-blue-800 underline">Preuzmi</a>
                  <span className="text-slate-500">{w.drawingFileName}</span>
                </span>
              ) : "Nema"}
            </Info>
            <div className="col-span-2 sm:col-span-4"><Info label="Napomena">{w.notes ?? EMPTY}</Info></div>
          </dl>
        )}
      </div>
    </div>
  );
}
