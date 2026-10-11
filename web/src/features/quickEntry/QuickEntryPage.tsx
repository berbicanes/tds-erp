import { useMemo, useState } from "react";
import { Link } from "react-router";
import { WarningList } from "../../components/ErrorBanner";
import { Stepper } from "../../components/form/Stepper";
import { useUnsavedChanges } from "../../components/UnsavedChanges";
import { ApiError } from "../../lib/api";
import { todaySarajevo } from "../../lib/date";
import { isString, isStringArray, pushRecent, readMemory, writeMemory } from "../../lib/deviceMemory";
import { formatDate, parseDecimal } from "../../lib/format";
import { labelOf, workOrderStatus } from "../../lib/labels";
import { itemTitle, qtyWithUnit } from "../../lib/material";
import { useEquipmentOptions } from "../equipment/api";
import { useSaveProductionEntry } from "../production/api";
import { useEmployeeOptions } from "../settings/api";
import { useWorkOrder, useWorkOrderOptions, type WorkOrderOption } from "../workOrders/api";
import { useWorkOrderMaterials } from "../workOrders/materialsApi";
import { quickEntryProblems, PRODUCTION_STATUSES } from "./quickEntry";

const big = "min-h-12 rounded border px-3 text-left";

function OrderPicker({ orders, recent, onPick }: { orders: WorkOrderOption[]; recent: string[]; onPick: (o: WorkOrderOption) => void }) {
  const [q, setQ] = useState("");
  const shown = useMemo(() => {
    const t = q.trim().toLocaleLowerCase("bs");
    const match = (o: WorkOrderOption) => !t || `${o.number} ${o.customer} ${o.productDescription}`.toLocaleLowerCase("bs").includes(t);
    const recentOrders = recent.map((id) => orders.find((o) => o.id === id)).filter((o): o is WorkOrderOption => !!o);
    return [...recentOrders, ...orders.filter((o) => !recent.includes(o.id))].filter(match);
  }, [orders, recent, q]);
  return (
    <div className="space-y-2">
      <input type="search" value={q} onChange={(e) => setQ(e.target.value)} placeholder="Broj naloga, kupac ili proizvod" aria-label="Pretraga naloga"
        className="h-12 w-full rounded border border-slate-300 px-3" />
      {shown.length === 0 && <p className="text-sm text-slate-600">{orders.length === 0 ? "Nema naloga u proizvodnji." : "Nema naloga za ovu pretragu."}</p>}
      <ul className="space-y-2">
        {shown.map((o) => (
          <li key={o.id}>
            <button type="button" onClick={() => onPick(o)} className={`${big} w-full border-slate-300 py-2`}>
              <span className="flex flex-wrap items-baseline gap-x-2">
                <span className="font-semibold tabular-nums">{o.number}</span>
                <span>{o.customer}</span>
                {recent.includes(o.id) && <span className="text-xs text-blue-800">nedavno</span>}
                <span className="ml-auto text-xs text-slate-600">{labelOf(workOrderStatus, o.status).text}</span>
              </span>
              <span className="block text-sm text-slate-600">{o.productDescription}</span>
            </button>
          </li>
        ))}
      </ul>
    </div>
  );
}

/** UI §4 — the most important screen: one entry in under 20 seconds, thumbs and gloves. */
export function QuickEntryPage() {
  const orders = useWorkOrderOptions();
  const employees = useEmployeeOptions();
  const machines = useEquipmentOptions("Masina");
  const save = useSaveProductionEntry(undefined);

  const [orderId, setOrderId] = useState<string | null>(null);
  const [recent, setRecent] = useState<string[]>(() => readMemory("quick.recentOrders", isStringArray) ?? []);
  const [operatorId, setOperatorId] = useState<string | null>(() => readMemory("quick.operator", isString));
  const [equipmentIds, setEquipmentIds] = useState<string[]>(() => readMemory("quick.equipment", isStringArray) ?? []);
  const [lotId, setLotId] = useState<string | null>(null);
  const [laser, setLaser] = useState("");
  const [pressed, setPressed] = useState("");
  const [good, setGood] = useState("");
  const [scrap, setScrap] = useState("");
  const [hours, setHours] = useState("");
  const [consumed, setConsumed] = useState("");
  const [note, setNote] = useState("");
  const [more, setMore] = useState(false);
  const [problems, setProblems] = useState<string[]>([]);
  const [saved, setSaved] = useState<{ number: string; warnings: string[] } | null>(null);

  const wo = useWorkOrder(orderId ?? undefined);
  const materials = useWorkOrderMaterials(orderId ?? "");
  const productionOrders = (orders.data ?? []).filter((o) => PRODUCTION_STATUSES.includes(o.status));
  const order = orders.data?.find((o) => o.id === orderId);
  const lots = (materials.data ?? []).filter((m) => m.receiptDecision === "Prihvacen");
  const finalMissing = wo.data ? !wo.data.finalInspectionPassed : false;
  const activeMachines = (machines.data ?? []);
  const operatorKnown = !!employees.data?.some((e) => e.id === operatorId);
  const needLot = (Number(laser) || 0) > 0 || (parseDecimal(consumed) ?? 0) > 0;
  const dirty = [laser, pressed, good, scrap, consumed, note].some((v) => v.trim() !== "");
  const markSaved = useUnsavedChanges(dirty);

  const pickOrder = (o: WorkOrderOption) => {
    setOrderId(o.id);
    setLotId(null);
    setGood("");
    setSaved(null);
  };

  const toggleMachine = (id: string) => {
    const next = equipmentIds.includes(id) ? equipmentIds.filter((x) => x !== id) : [...equipmentIds, id];
    setEquipmentIds(next);
    writeMemory("quick.equipment", next);
  };

  const submit = () => {
    const input = {
      orderId, operatorId: operatorKnown ? operatorId : null, equipmentIds: equipmentIds.filter((id) => activeMachines.some((m) => m.id === id)),
      lotId: needLot ? lotId ?? (lots.length === 1 ? lots[0]!.materialLotId : null) : null,
      laser, pressed, good: finalMissing ? "" : good, scrap, hours, consumed,
    };
    const found = quickEntryProblems(input);
    setProblems(found);
    setSaved(null);
    if (found.length > 0) return;
    save.mutate({
      workOrderId: orderId!, date: todaySarajevo(), operatorEmployeeId: input.operatorId!, equipmentIds: input.equipmentIds,
      materialLotId: input.lotId, laserCutQty: Number(laser) || 0, pressedQty: Number(pressed) || 0, goodQty: Number(input.good) || 0,
      scrapQty: Number(scrap) || 0, laborHours: parseDecimal(hours) ?? 0, materialConsumed: parseDecimal(consumed), note: note.trim() || null, version: 0,
    }, {
      onSuccess: (r) => {
        markSaved();
        const nextRecent = pushRecent(recent, orderId!);
        setRecent(nextRecent);
        writeMemory("quick.recentOrders", nextRecent);
        setSaved({ number: r.data.number, warnings: r.warnings });
        // Same order and operator stay for the next entry (UI §4).
        setLaser(""); setPressed(""); setGood(""); setScrap(""); setHours(""); setConsumed(""); setNote("");
        window.scrollTo({ top: 0 });
      },
    });
  };

  const serverError = save.error instanceof ApiError
    ? [save.error.message, ...Object.values(save.error.fieldErrors).flat()].filter((m, i, all) => all.indexOf(m) === i)
    : save.error ? [save.error.message] : [];

  return (
    <div className="mx-auto max-w-xl space-y-5 px-4 py-4">
      <div className="flex items-baseline justify-between">
        <h1 className="text-lg font-semibold">Brzi unos</h1>
        <span className="text-sm text-slate-600">Danas, {formatDate(todaySarajevo())}</span>
      </div>

      {saved && (
        <div role="status" aria-live="polite" className="space-y-2 rounded bg-green-700 p-3 font-medium text-white">
          <p>Spremljeno {saved.number}</p>
        </div>
      )}
      {saved && <WarningList warnings={saved.warnings} />}

      <section className="space-y-2">
        <h2 className="text-sm font-semibold">1. Nalog *</h2>
        {order ? (
          <div className="flex items-center gap-2 rounded border border-blue-700 bg-blue-50 p-3">
            <div className="min-w-0">
              <p className="font-semibold tabular-nums">{order.number} · {order.customer}</p>
              <p className="truncate text-sm text-slate-700">{order.productDescription}</p>
            </div>
            <button type="button" onClick={() => setOrderId(null)} className="ml-auto min-h-12 shrink-0 rounded border border-slate-300 bg-white px-3 text-sm">Promijeni</button>
          </div>
        ) : orders.isPending ? <p className="text-sm text-slate-600">Učitavanje naloga…</p> : (
          <OrderPicker orders={productionOrders} recent={recent} onPick={pickOrder} />
        )}
      </section>

      {order && (
        <>
          <section className="space-y-2">
            <label htmlFor="qe-operator" className="block text-sm font-semibold">2. Operater *</label>
            <select id="qe-operator" value={operatorKnown ? operatorId ?? "" : ""} className="h-12 w-full rounded border border-slate-300 bg-white px-3"
              onChange={(e) => { setOperatorId(e.target.value || null); writeMemory("quick.operator", e.target.value); }}>
              <option value="">Odaberi operatera…</option>
              {(employees.data ?? []).map((e) => <option key={e.id} value={e.id}>{e.fullName}</option>)}
            </select>
          </section>

          <section className="space-y-2">
            <h2 className="text-sm font-semibold">3. Oprema *</h2>
            <div className="flex flex-wrap gap-2">
              {activeMachines.map((m) => {
                const on = equipmentIds.includes(m.id);
                return (
                  <button key={m.id} type="button" aria-pressed={on} title={m.name} onClick={() => toggleMachine(m.id)}
                    className={`min-h-12 min-w-20 rounded border px-3 font-medium ${on ? "border-blue-700 bg-blue-700 text-white" : "border-slate-300 bg-white"}`}>
                    {m.code}
                  </button>
                );
              })}
            </div>
          </section>

          <section className="grid grid-cols-1 gap-4 min-[420px]:grid-cols-2">
            <Stepper id="qe-laser" label="Laser — izrezano" value={laser} onChange={setLaser} />
            <Stepper id="qe-pressed" label="Presa — obrađeno" value={pressed} onChange={setPressed} />
            <Stepper id="qe-good" label="Gotovo ispravno" value={finalMissing ? "" : good} onChange={setGood} disabled={finalMissing}
              hint={finalMissing ? "Gotovi ispravni komadi se upisuju tek nakon prihvaćene završne kontrole." : undefined} />
            <Stepper id="qe-scrap" label="Škart / za doradu" value={scrap} onChange={setScrap} />
          </section>

          {needLot && (
            <section className="space-y-2">
              <h2 className="text-sm font-semibold">Lot materijala *</h2>
              {lots.length === 0 ? (
                <p className="rounded bg-amber-50 p-3 text-sm text-amber-900">
                  Na nalogu nema prihvaćenog lota. Veži lot na nalogu (tab Materijal) ili ga prihvati u <Link to="/zalihe/lotovi" className="underline">Zalihama</Link>.
                </p>
              ) : (
                <div className="space-y-2">
                  {lots.map((m) => {
                    const on = (lotId ?? (lots.length === 1 ? lots[0]!.materialLotId : null)) === m.materialLotId;
                    return (
                      <button key={m.materialLotId} type="button" aria-pressed={on} onClick={() => setLotId(m.materialLotId)}
                        className={`${big} w-full ${on ? "border-blue-700 bg-blue-50" : "border-slate-300"}`}>
                        <span className="font-semibold">{m.lotNumber}</span>{" "}
                        <span className="text-sm text-slate-700">{itemTitle({ name: m.itemName, grade: m.itemGrade, thicknessMm: m.itemThicknessMm })} · na stanju {qtyWithUnit(m.lotOnHand, m.unit)}</span>
                      </button>
                    );
                  })}
                </div>
              )}
            </section>
          )}

          <section>
            <Stepper id="qe-hours" label="Radni sati *" value={hours} onChange={setHours} step={0.5} decimals />
          </section>

          <section>
            <button type="button" onClick={() => setMore(!more)} aria-expanded={more} className="min-h-12 text-sm font-medium text-blue-800 underline">
              {more ? "Sakrij utrošak i napomenu" : "Utrošak materijala i napomena"}
            </button>
            {more && (
              <div className="mt-2 space-y-3">
                <div>
                  <label htmlFor="qe-consumed" className="mb-1 block text-sm font-medium">Utrošak materijala{lots[0] ? ` (${lots[0].unit})` : ""}</label>
                  <input id="qe-consumed" inputMode="decimal" value={consumed} onChange={(e) => setConsumed(e.target.value)} className="h-12 w-full rounded border border-slate-300 px-3" />
                  <p className="mt-1 text-xs text-slate-600">Prazno = zaliha se ne skida.</p>
                </div>
                <div>
                  <label htmlFor="qe-note" className="mb-1 block text-sm font-medium">Napomena</label>
                  <textarea id="qe-note" value={note} maxLength={1000} onChange={(e) => setNote(e.target.value)} className="min-h-20 w-full rounded border border-slate-300 px-3 py-2" />
                </div>
              </div>
            )}
          </section>

          {(problems.length > 0 || serverError.length > 0) && (
            <ul role="alert" className="space-y-1 rounded border border-red-200 bg-red-50 p-3 text-sm text-red-800">
              {[...problems, ...serverError].map((p) => <li key={p}>{p}</li>)}
            </ul>
          )}
          <button type="button" onClick={submit} disabled={save.isPending}
            className="min-h-14 w-full rounded bg-blue-700 text-lg font-semibold text-white disabled:opacity-60">
            {save.isPending ? "Spremanje…" : "Spremi"}
          </button>
        </>
      )}
    </div>
  );
}
