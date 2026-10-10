import { useState } from "react";
import { Controller, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { useLocation, useNavigate, useParams } from "react-router";
import { Field, inputClass, textareaClass } from "../../components/form/Field";
import { Combobox } from "../../components/form/Combobox";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { ErrorBanner, WarningList } from "../../components/ErrorBanner";
import { PageHeader } from "../../components/PageHeader";
import { ApiError } from "../../lib/api";
import { hasRole, useMe } from "../../lib/auth";
import { todaySarajevo } from "../../lib/date";
import { formatDate } from "../../lib/format";
import { userLabel } from "../../lib/labels";
import { itemTitle, qtyWithUnit } from "../../lib/material";
import { dateInput, decimalInput, intInput, optionalText } from "../../lib/schema";
import { ChangedOnServerNotice, useUnsavedChanges } from "../../components/UnsavedChanges";
import { useServerForm } from "../../hooks/useServerForm";
import { useEmployeeOptions } from "../settings/api";
import { useEquipmentOptions } from "../equipment/api";
import { useLots } from "../stock/lotsApi";
import { useWorkOrder, useWorkOrderOptions } from "../workOrders/api";
import { useProductionEntry, useSaveProductionEntry, useVoidProductionEntry } from "./api";

// Mirrors SaveProductionEntryValidator; the server checks all DOMAIN §5 rules again.
const qty = intInput({ min: 0, minMessage: "Ne može biti negativno." });
const schema = z
  .object({
    workOrderId: z.string().nullable().refine((v) => !!v, "Odaberi nalog."),
    date: dateInput("Unesi datum."),
    operatorEmployeeId: z.string().nullable().refine((v) => !!v, "Odaberi operatera."),
    equipmentIds: z.array(z.string()).min(1, "Odaberi barem jednu mašinu."),
    materialLotId: z.string().nullable(),
    laserCutQty: qty,
    pressedQty: qty,
    goodQty: qty,
    scrapQty: qty,
    laborHours: decimalInput({ required: "Unesi radne sate.", min: 0, max: 24, maxMessage: "Radni sati moraju biti između 0 i 24." }),
    materialConsumed: decimalInput({ min: 0.001, minMessage: "Utrošak mora biti veći od 0 ili prazan." }),
    note: optionalText(1000),
  })
  .superRefine((v, ctx) => {
    const total = (v.laserCutQty ?? 0) + (v.pressedQty ?? 0) + (v.goodQty ?? 0) + (v.scrapQty ?? 0);
    if (total === 0) ctx.addIssue({ code: "custom", path: ["pressedQty"], message: "Unesi barem jednu količinu: laser, presa, gotovo ili škart." });
    if (((v.laserCutQty ?? 0) > 0 || (v.materialConsumed ?? 0) > 0) && !v.materialLotId)
      ctx.addIssue({ code: "custom", path: ["materialLotId"], message: "Odaberi lot materijala — obavezan je kad se reže ili troši materijal." });
  });
type FormIn = z.input<typeof schema>;
type FormOut = z.output<typeof schema>;

const accepted = { page: 1, sort: "lotNumber", q: "", filters: { receiptDecision: ["Prihvacen"] } };

export function ProductionEntryFormPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const me = useMe();
  const entry = useProductionEntry(id);
  const orders = useWorkOrderOptions();
  const employees = useEmployeeOptions();
  const machines = useEquipmentOptions("Masina");
  const lots = useLots(accepted, "&pageSize=200");
  const save = useSaveProductionEntry(id);
  const voidEntry = useVoidProductionEntry();
  const [warnings, setWarnings] = useState<string[]>([]);
  const [voidOpen, setVoidOpen] = useState(false);
  const [voidReason, setVoidReason] = useState("");
  const presetOrder = new URLSearchParams(location.search).get("nalog");

  const form = useForm<FormIn, unknown, FormOut>({
    resolver: zodResolver(schema),
    defaultValues: {
      workOrderId: presetOrder, date: todaySarajevo(), operatorEmployeeId: null, equipmentIds: [], materialLotId: null,
      laserCutQty: "", pressedQty: "", goodQty: "", scrapQty: "", laborHours: "", materialConsumed: "", note: "",
    },
  });
  const { register, handleSubmit, control, setError, formState: { errors, isDirty } } = form;

  // Never overwrite unsaved input with refetched data (lib/formSync.ts).
  const sync = useServerForm(form, entry.data, (e) => ({
        workOrderId: e.workOrderId, date: e.date, operatorEmployeeId: e.operatorEmployeeId, equipmentIds: e.equipmentIds,
        materialLotId: e.materialLotId, laserCutQty: String(e.laserCutQty), pressedQty: String(e.pressedQty), goodQty: String(e.goodQty),
        scrapQty: String(e.scrapQty), laborHours: String(e.laborHours).replace(".", ","),
        materialConsumed: e.materialConsumed == null ? "" : String(e.materialConsumed).replace(".", ","), note: e.note ?? "",
      }));

  const markSaved = useUnsavedChanges(isDirty);
  const [workOrderId, lotId] = useWatch({ control, name: ["workOrderId", "materialLotId"] });
  const wo = useWorkOrder(workOrderId ?? undefined);
  const lot = lots.data?.items.find((l) => l.id === lotId);
  const finalMissing = wo.data ? !wo.data.finalInspectionPassed : false;
  const readOnly = entry.data?.isVoided ?? false;

  const onSubmit = handleSubmit((v) =>
    save.mutate(
      {
        workOrderId: v.workOrderId!, date: v.date!, operatorEmployeeId: v.operatorEmployeeId!, equipmentIds: v.equipmentIds,
        materialLotId: v.materialLotId, laserCutQty: v.laserCutQty ?? 0, pressedQty: v.pressedQty ?? 0, goodQty: v.goodQty ?? 0,
        scrapQty: v.scrapQty ?? 0, laborHours: v.laborHours ?? 0, materialConsumed: v.materialConsumed, note: v.note,
        version: sync.baseVersion,
      },
      {
        onSuccess: (r) => {
          sync.markSaved(r.data);
          setWarnings(r.warnings);
          markSaved();
          if (r.warnings.length === 0) navigate("/proizvodnja");
          else navigate(`/proizvodnja/${r.data.id}`, { replace: true });
        },
        onError: (e) => {
          if (e instanceof ApiError) for (const [f, m] of Object.entries(e.fieldErrors)) setError(f as keyof FormIn, { message: m[0] ?? "" });
        },
      },
    ),
  );

  if (id && entry.isPending) return <p className="p-4 text-slate-600">Učitavanje…</p>;
  if (id && entry.isError) return <div className="p-4"><ErrorBanner error={entry.error} /></div>;

  const e = entry.data;
  return (
    <>
      <PageHeader title={e ? `Dnevni zapis ${e.number}` : "Novi dnevni zapis"} />
      <form onSubmit={onSubmit} noValidate className="max-w-3xl space-y-4 px-4 py-4">
        {e && (
          <p className="text-sm text-slate-600">
            Unio: {userLabel(e.createdByName)} · {formatDate(e.date)}
            {e.isVoided && <span className="ml-2 font-medium text-slate-800">Poništen: {e.voidedReason}</span>}
          </p>
        )}
        <WarningList warnings={warnings} />
        <fieldset disabled={readOnly} className="space-y-4">
          <Field label="Nalog" htmlFor="workOrderId" required error={errors.workOrderId?.message} hint={e ? "Nalog se na postojećem zapisu ne mijenja." : undefined}>
            <Controller control={control} name="workOrderId" render={({ field }) => (
              <Combobox id="workOrderId" value={field.value} onChange={field.onChange} invalid={!!errors.workOrderId}
                options={[
                  ...(e && !orders.data?.some((o) => o.id === e.workOrderId) ? [{ value: e.workOrderId, label: e.workOrderNumber, detail: e.customer }] : []),
                  ...(orders.data ?? []).map((o) => ({ value: o.id, label: o.number, detail: `${o.customer} · ${o.productDescription}` })),
                ]} />
            )} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Datum" htmlFor="date" required error={errors.date?.message}>
              <input id="date" type="date" max={todaySarajevo()} className={inputClass} {...register("date")} />
            </Field>
            <Field label="Operater" htmlFor="operatorEmployeeId" required error={errors.operatorEmployeeId?.message}>
              <Controller control={control} name="operatorEmployeeId" render={({ field }) => (
                <Combobox id="operatorEmployeeId" value={field.value} onChange={field.onChange} invalid={!!errors.operatorEmployeeId}
                  options={(employees.data ?? []).map((x) => ({ value: x.id, label: x.fullName }))} />
              )} />
            </Field>
          </div>
          <Field label="Oprema" htmlFor="equipment" required error={errors.equipmentIds?.message}>
            <Controller control={control} name="equipmentIds" render={({ field }) => (
              <div id="equipment" className="flex flex-wrap gap-2">
                {(machines.data ?? []).map((m) => {
                  const on = field.value.includes(m.id);
                  return (
                    <button key={m.id} type="button" aria-pressed={on} title={m.name}
                      onClick={() => field.onChange(on ? field.value.filter((x) => x !== m.id) : [...field.value, m.id])}
                      className={`min-h-12 min-w-16 rounded border px-3 text-sm ${on ? "border-blue-700 bg-blue-700 text-white" : "border-slate-300"}`}>
                      {m.code}
                    </button>
                  );
                })}
              </div>
            )} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-4">
            <Field label="Laser — izrezano" htmlFor="laserCutQty" error={errors.laserCutQty?.message}>
              <input id="laserCutQty" inputMode="numeric" className={inputClass} {...register("laserCutQty")} />
            </Field>
            <Field label="Presa — obrađeno" htmlFor="pressedQty" error={errors.pressedQty?.message}>
              <input id="pressedQty" inputMode="numeric" className={inputClass} {...register("pressedQty")} />
            </Field>
            <Field label="Gotovo ispravno" htmlFor="goodQty" error={errors.goodQty?.message}
              hint={finalMissing ? "Gotovi ispravni komadi se upisuju tek nakon prihvaćene završne kontrole." : undefined}>
              <input id="goodQty" inputMode="numeric" disabled={finalMissing} className={`${inputClass} disabled:bg-slate-100`} {...register("goodQty")} />
            </Field>
            <Field label="Škart / za doradu" htmlFor="scrapQty" error={errors.scrapQty?.message}>
              <input id="scrapQty" inputMode="numeric" className={inputClass} {...register("scrapQty")} />
            </Field>
          </div>
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Lot materijala (prihvaćen)" htmlFor="materialLotId" error={errors.materialLotId?.message} hint="Obavezan kad se reže ili troši materijal.">
              <Controller control={control} name="materialLotId" render={({ field }) => (
                <Combobox id="materialLotId" value={field.value} onChange={field.onChange} allowEmpty emptyLabel="Bez lota" invalid={!!errors.materialLotId}
                  options={(lots.data?.items ?? []).map((l) => ({
                    value: l.id, label: l.lotNumber,
                    detail: `${itemTitle({ name: l.itemName, grade: l.itemGrade, thicknessMm: l.itemThicknessMm })} · ${qtyWithUnit(l.onHand, l.unit)}`,
                  }))} />
              )} />
            </Field>
            <Field label={`Utrošak materijala${lot ? ` (${lot.unit})` : ""}`} htmlFor="materialConsumed" error={errors.materialConsumed?.message}
              hint="Prazno = zaliha se ne skida.">
              <input id="materialConsumed" inputMode="decimal" className={inputClass} {...register("materialConsumed")} />
            </Field>
            <Field label="Radni sati" htmlFor="laborHours" required error={errors.laborHours?.message}>
              <input id="laborHours" inputMode="decimal" className={inputClass} {...register("laborHours")} />
            </Field>
          </div>
          <Field label="Napomena" htmlFor="note" error={errors.note?.message}>
            <textarea id="note" className={textareaClass} {...register("note")} />
          </Field>
        </fieldset>

        <ChangedOnServerNotice show={sync.changedOnServer} onDiscard={sync.discard} />
        <ErrorBanner error={save.error instanceof ApiError && Object.keys(save.error.fieldErrors).length ? null : save.error ?? voidEntry.error} onRefresh={() => { save.reset(); void sync.reload(entry.refetch); }} />

        <div className="flex flex-wrap gap-2">
          {!readOnly && (
            <button type="submit" disabled={save.isPending} className="min-h-12 rounded bg-blue-700 px-5 font-medium text-white disabled:opacity-60">
              {save.isPending ? "Spremanje…" : "Spremi"}
            </button>
          )}
          <button type="button" onClick={() => navigate("/proizvodnja")} className="min-h-12 rounded border border-slate-300 px-5">Nazad</button>
          {e && !e.isVoided && (hasRole(me.data, "Admin", "Kontrolor") || e.createdBy === me.data?.id) && (
            <button type="button" onClick={() => setVoidOpen(true)} className="ml-auto min-h-12 rounded border border-red-300 px-5 text-red-800">Poništi zapis</button>
          )}
        </div>
        {e && (
          <ConfirmDialog open={voidOpen} title={`Poništiti zapis ${e.number}?`} confirmText="Poništi" danger onCancel={() => setVoidOpen(false)}
            onConfirm={() => {
              if (!voidReason.trim()) return;
              setVoidOpen(false);
              voidEntry.mutate({ id: e.id, reason: voidReason.trim(), version: e.version });
            }}>
            <p className="mb-2">Poništava se i utrošak materijala sa ovog zapisa.</p>
            <label htmlFor="void-reason" className="block text-sm font-medium">Razlog *</label>
            <input id="void-reason" className={inputClass} value={voidReason} maxLength={300} onChange={(ev) => setVoidReason(ev.target.value)} />
          </ConfirmDialog>
        )}
      </form>
    </>
  );
}
