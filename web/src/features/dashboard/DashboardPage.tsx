import { useState, type ReactNode } from "react";
import { Link } from "react-router";
import { PageHeader } from "../../components/PageHeader";
import { Badge } from "../../components/Badge";
import { ErrorBanner } from "../../components/ErrorBanner";
import { ProgressBar, QualityMarks } from "../../components/ProgressBar";
import { kpiLinks, warningHref, warningText } from "../../lib/dashboard";
import { EMPTY, formatHours, formatPieces, formatQuantity, formatTime, formatDate } from "../../lib/format";
import { labelOf, priority, stockLevel, workOrderStatus } from "../../lib/labels";
import { dueTone, isFinished } from "../../lib/workOrder";
import { qtyWithUnit } from "../../lib/material";
import { useDashboard, type Dashboard } from "./api";
import { useAddNote, useSetNoteDone } from "./notesApi";
import { ProductionChart } from "./ProductionChart";

function Card({ title, children, footer, className = "" }: { title: string; children: ReactNode; footer?: ReactNode; className?: string }) {
  return (
    <section className={`min-w-0 space-y-2 ${className}`}>
      <h2 className="text-sm font-semibold uppercase tracking-wide text-slate-600">{title}</h2>
      {children}
      {footer}
    </section>
  );
}

function Tiles({ d }: { d: Dashboard }) {
  const links = kpiLinks(d.today);
  const k = d.kpi;
  const tiles: { label: string; value: number; href: string; alert?: boolean; note?: string }[] = [
    { label: "Aktivni nalozi", value: k.activeOrders, href: links.activeOrders },
    { label: "Kasne", value: k.overdueOrders, href: links.overdueOrders, alert: k.overdueOrders > 0 },
    { label: "Rok u 7 dana", value: k.dueIn7Days, href: links.dueIn7Days },
    { label: "Hitni", value: k.urgentOrders, href: links.urgentOrders },
    { label: "Danas gotovo", value: k.goodToday, href: links.goodToday, ...(k.entriesToday === 0 ? { note: "Još nema zapisa za danas" } : {}) },
    { label: "Materijal ispod minimuma", value: k.itemsBelowMinimum, href: links.itemsBelowMinimum, alert: k.itemsBelowMinimum > 0 },
  ];
  return (
    <div className="grid grid-cols-2 gap-2 sm:grid-cols-3 lg:grid-cols-6">
      {tiles.map((t) => (
        <Link key={t.label} to={t.href}
          className={`flex min-h-24 flex-col justify-between rounded border p-3 hover:bg-slate-50 ${t.alert ? "border-red-300 bg-red-50" : "border-slate-200"}`}>
          <span className="text-xs text-slate-600">{t.label}</span>
          <span className={`text-3xl font-semibold tabular-nums ${t.alert ? "text-red-700" : ""}`}>{formatPieces(t.value)}</span>
          {t.note && <span className="text-xs text-slate-500">{t.note}</span>}
        </Link>
      ))}
    </div>
  );
}

function TodayTable({ d }: { d: Dashboard }) {
  if (d.todayEntries.length === 0)
    return <p className="rounded border border-slate-200 p-3 text-sm text-slate-600">Danas još nema dnevnih zapisa.</p>;
  const t = d.todayTotals;
  return (
    <div className="overflow-x-auto rounded border border-slate-200">
      <table className="w-full text-sm cards-sm">
        <thead className="bg-slate-50 text-left text-xs text-slate-600">
          <tr>
            <th className="px-2 py-1.5 font-medium">Operater</th>
            <th className="px-2 py-1.5 font-medium">Nalog</th>
            <th className="px-2 py-1.5 text-right font-medium">Laser</th>
            <th className="px-2 py-1.5 text-right font-medium">Presa</th>
            <th className="px-2 py-1.5 text-right font-medium">Gotovo</th>
            <th className="px-2 py-1.5 text-right font-medium">Škart</th>
            <th className="px-2 py-1.5 text-right font-medium">Sati</th>
          </tr>
        </thead>
        <tbody>
          {d.todayEntries.map((e) => (
            <tr key={e.id} className="border-t border-slate-100">
              <td data-label="Operater" className="px-2 py-1"><Link to={`/proizvodnja/${e.id}`} className="text-blue-800 hover:underline">{e.operatorName}</Link></td>
              <td data-label="Nalog" className="px-2 py-1"><Link to={`/nalozi/${e.workOrderId}`} className="text-blue-800 hover:underline">{e.workOrderNumber}</Link><span className="block text-xs text-slate-500">{e.customer}</span></td>
              <td data-label="Laser" className="px-2 py-1 text-right tabular-nums">{formatPieces(e.laserCutQty)}</td>
              <td data-label="Presa" className="px-2 py-1 text-right tabular-nums">{formatPieces(e.pressedQty)}</td>
              <td data-label="Gotovo" className="px-2 py-1 text-right tabular-nums">{formatPieces(e.goodQty)}</td>
              <td data-label="Škart" className="px-2 py-1 text-right tabular-nums">{formatPieces(e.scrapQty)}</td>
              <td data-label="Sati" className="px-2 py-1 text-right tabular-nums">{formatHours(e.laborHours)}</td>
            </tr>
          ))}
        </tbody>
        <tfoot className="border-t-2 border-slate-300 font-semibold">
          <tr>
            <td className="px-2 py-1" colSpan={2}>Ukupno</td>
            <td data-label="Laser" className="px-2 py-1 text-right tabular-nums">{formatPieces(t.laserCutQty)}</td>
            <td data-label="Presa" className="px-2 py-1 text-right tabular-nums">{formatPieces(t.pressedQty)}</td>
            <td data-label="Gotovo" className="px-2 py-1 text-right tabular-nums">{formatPieces(t.goodQty)}</td>
            <td data-label="Škart" className="px-2 py-1 text-right tabular-nums">{formatPieces(t.scrapQty)}</td>
            <td data-label="Sati" className="px-2 py-1 text-right tabular-nums">{formatHours(t.laborHours)}</td>
          </tr>
        </tfoot>
      </table>
    </div>
  );
}

function Warnings({ d }: { d: Dashboard }) {
  if (d.warnings.length === 0) return <p className="rounded border border-slate-200 p-3 text-sm text-slate-600">Nema upozorenja.</p>;
  return (
    <ul className="divide-y divide-slate-100 rounded border border-slate-200">
      {d.warnings.map((w) => (
        <li key={`${w.kind}-${w.entityId}`}>
          <Link to={warningHref(w)} className="flex min-h-11 items-start gap-2 px-3 py-2 text-sm hover:bg-slate-50">
            <span aria-hidden className={`mt-0.5 font-bold ${w.severity === "red" ? "text-red-700" : "text-amber-700"}`}>{w.severity === "red" ? "!" : "•"}</span>
            <span className="sr-only">{w.severity === "red" ? "Hitno: " : "Pažnja: "}</span>
            <span className={w.severity === "red" ? "text-red-800" : "text-slate-800"}>{warningText(w, d.today)}</span>
          </Link>
        </li>
      ))}
    </ul>
  );
}

function OrdersByDue({ d }: { d: Dashboard }) {
  if (d.ordersByDue.items.length === 0) return <p className="rounded border border-slate-200 p-3 text-sm text-slate-600">Nema aktivnih naloga.</p>;
  return (
    <div className="overflow-x-auto rounded border border-slate-200">
      <table className="w-full text-sm cards-sm">
        <thead className="bg-slate-50 text-left text-xs text-slate-600">
          <tr>
            <th className="px-2 py-1.5 font-medium">Broj</th>
            <th className="px-2 py-1.5 font-medium">Kupac</th>
            <th className="px-2 py-1.5 font-medium">Proizvod</th>
            <th className="px-2 py-1.5 font-medium">Status</th>
            <th className="px-2 py-1.5 font-medium">Prioritet</th>
            <th className="px-2 py-1.5 font-medium">Napredak</th>
            <th className="px-2 py-1.5 font-medium">Rok</th>
            <th className="px-2 py-1.5 font-medium">Kvalitet</th>
          </tr>
        </thead>
        <tbody>
          {d.ordersByDue.items.map((w) => {
            const tone = dueTone(w.dueDate, w.isOverdue, isFinished(w.status), d.today);
            return (
              <tr key={w.id} className="border-t border-slate-100">
                <td data-label="Broj" className="px-2 py-1"><Link to={`/nalozi/${w.id}`} className="font-medium text-blue-800 hover:underline">{w.number}</Link></td>
                <td data-label="Kupac" className="px-2 py-1">{w.customer}</td>
                <td data-label="Proizvod" className="px-2 py-1"><span className="line-clamp-1">{w.productDescription}</span></td>
                <td data-label="Status" className="px-2 py-1"><Badge label={labelOf(workOrderStatus, w.status)} /></td>
                <td data-label="Prioritet" className="px-2 py-1">{w.priority === "Hitno" ? <Badge label={labelOf(priority, w.priority)} /> : "Normalan"}</td>
                <td data-label="Napredak" className="px-2 py-1"><ProgressBar completed={w.completedQty} ordered={w.orderedQty} percent={w.progressPercent} /></td>
                <td data-label="Rok" className={`whitespace-nowrap px-2 py-1 ${tone === "red" ? "font-semibold text-red-700" : tone === "amber" ? "font-semibold text-amber-800" : ""}`}>{formatDate(w.dueDate)}</td>
                <td data-label="Kvalitet" className="px-2 py-1"><QualityMarks laser={w.laserFirstPieceApproved} press={w.pressFirstPieceApproved} final={w.finalInspectionPassed} /></td>
              </tr>
            );
          })}
        </tbody>
      </table>
    </div>
  );
}

function StockTable({ d }: { d: Dashboard }) {
  if (d.stock.items.length === 0) return <p className="rounded border border-slate-200 p-3 text-sm text-slate-600">Nema artikala.</p>;
  return (
    <div className="overflow-x-auto rounded border border-slate-200">
      <table className="w-full text-sm cards-sm">
        <thead className="bg-slate-50 text-left text-xs text-slate-600">
          <tr>
            <th className="px-2 py-1.5 font-medium">Artikal</th>
            <th className="px-2 py-1.5 font-medium">Kvalitet</th>
            <th className="px-2 py-1.5 text-right font-medium">Debljina mm</th>
            <th className="px-2 py-1.5 text-right font-medium">Na stanju</th>
            <th className="px-2 py-1.5 text-right font-medium">Rezervisano</th>
            <th className="px-2 py-1.5 text-right font-medium">Raspoloživo</th>
            <th className="px-2 py-1.5 text-right font-medium">Minimum</th>
            <th className="px-2 py-1.5 font-medium">Nivo</th>
          </tr>
        </thead>
        <tbody>
          {d.stock.items.map((s) => (
            <tr key={s.materialItemId} className="border-t border-slate-100">
              <td data-label="Artikal" className="px-2 py-1"><Link to={`/zalihe/artikli/${s.materialItemId}`} className="text-blue-800 hover:underline">{s.name}</Link></td>
              <td data-label="Kvalitet" className="px-2 py-1">{s.grade ?? EMPTY}</td>
              <td data-label="Debljina mm" className="px-2 py-1 text-right tabular-nums">{s.thicknessMm == null ? EMPTY : formatQuantity(s.thicknessMm)}</td>
              <td data-label="Na stanju" className="px-2 py-1 text-right tabular-nums">{qtyWithUnit(s.onHand, s.unit)}</td>
              <td data-label="Rezervisano" className="px-2 py-1 text-right tabular-nums">{qtyWithUnit(s.reserved, s.unit)}</td>
              <td data-label="Raspoloživo" className={`px-2 py-1 text-right tabular-nums ${s.available < 0 ? "font-semibold text-red-700" : ""}`}>{qtyWithUnit(s.available, s.unit)}</td>
              <td data-label="Minimum" className="px-2 py-1 text-right tabular-nums">{qtyWithUnit(s.minimumStock, s.unit)}</td>
              <td data-label="Nivo" className="px-2 py-1"><Badge label={labelOf(stockLevel, s.stockLevel)} /></td>
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Notes({ d }: { d: Dashboard }) {
  const add = useAddNote();
  const toggle = useSetNoteDone();
  const [text, setText] = useState("");
  return (
    <div className="space-y-2">
      <form onSubmit={(e) => { e.preventDefault(); if (text.trim()) add.mutate(text.trim(), { onSuccess: () => setText("") }); }}>
        <label htmlFor="new-note" className="sr-only">Nova bilješka</label>
        <input id="new-note" value={text} maxLength={300} onChange={(e) => setText(e.target.value)} placeholder="Nova bilješka + Enter"
          className="h-11 w-full rounded border border-slate-300 px-3 text-sm" />
      </form>
      <ErrorBanner error={add.error ?? toggle.error} />
      {d.notes.length === 0 ? <p className="text-sm text-slate-600">Nema bilješki.</p> : (
        <ul className="space-y-1">
          {d.notes.map((n) => (
            <li key={n.id}>
              <label className="flex min-h-11 items-center gap-3 text-sm">
                <input type="checkbox" className="h-5 w-5" checked={n.isDone} disabled={toggle.isPending}
                  onChange={(e) => toggle.mutate({ id: n.id, isDone: e.target.checked, version: n.version })} />
                <span className={n.isDone ? "text-slate-400 line-through" : ""}>{n.text}</span>
              </label>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** UI §3 — start screen. 12-column grid on desktop; one column in the same order on phones. */
export function DashboardPage() {
  const dash = useDashboard();
  const d = dash.data;
  return (
    <>
      <PageHeader title="Pregled">
        {d && <span className="text-sm text-slate-600 tabular-nums">Ažurirano {formatTime(d.generatedAt)}</span>}
      </PageHeader>
      <div className="space-y-6 px-4 py-4">
        <ErrorBanner error={dash.error} onRefresh={() => void dash.refetch()} />
        {dash.isPending && <p className="text-slate-600">Učitavanje…</p>}
        {d && (
          <>
            <Tiles d={d} />
            <div className="grid gap-6 lg:grid-cols-12">
              <Card title="Danas u proizvodnji" className="lg:col-span-8">
                <TodayTable d={d} />
                <ProductionChart days={d.chart} />
              </Card>
              <div className="space-y-6 lg:col-span-4">
                <Card title="Upozorenja"><Warnings d={d} /></Card>
                <Card title="Bilješke"><Notes d={d} /></Card>
              </div>
            </div>
            <Card title="Nalozi po roku" footer={<Link to="/nalozi?active=true&sort=dueDate" className="inline-block text-sm text-blue-800 underline">Svi nalozi ({formatPieces(d.ordersByDue.total)}) →</Link>}>
              <OrdersByDue d={d} />
            </Card>
            <Card title="Zalihe" footer={<Link to="/zalihe/artikli" className="inline-block text-sm text-blue-800 underline">Sve zalihe ({formatPieces(d.stock.total)}) →</Link>}>
              <StockTable d={d} />
            </Card>
          </>
        )}
      </div>
    </>
  );
}
