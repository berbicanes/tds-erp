import { useEffect } from "react";
import { Link, useNavigate, useParams, useSearchParams } from "react-router";
import { useQuery } from "@tanstack/react-query";
import { Controller, useForm, useWatch } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Badge } from "../../components/Badge";
import { ErrorBanner } from "../../components/ErrorBanner";
import { EvidenceFiles } from "../../components/EvidenceFiles";
import { HistoryList } from "../../components/HistoryList";
import { Combobox } from "../../components/form/Combobox";
import { Field, inputClass, textareaClass } from "../../components/form/Field";
import { ChangedOnServerNotice, useUnsavedChanges } from "../../components/UnsavedChanges";
import { useServerForm } from "../../hooks/useServerForm";
import { api, ApiError, type Paged } from "../../lib/api";
import { hasRole, useMe } from "../../lib/auth";
import { labelOf, ncSource, ncStatus, productDecision, type NcStatus } from "../../lib/labels";
import { dateInput, optionalText, requiredText } from "../../lib/schema";
import { useEmployeeOptions } from "../settings/api";
import { useSupplierOptions } from "../suppliers/api";
import { lotKey, type Lot } from "../stock/lotsApi";
import { useWorkOrderOptions } from "../workOrders/api";
import { useNc, useNcEvidence, useNcPrefill, useNcStatus, useSaveNc, type NonConformity, type Transition } from "./api";
import { VoidNcButton } from "./VoidNcButton";

// Mirrors SaveNcValidator.
const schema = z.object({
  source: z.enum(["Dobavljac", "UlazniMaterijal", "Laser", "Presanje", "ZavrsnaKontrola", "Kupac", "Audit", "Ostalo"]),
  description: requiredText(2000, "Opiši neusaglašenost."),
  containment: optionalText(2000),
  productDecision: z.enum(["CekaOdluku", "Dorada", "Skart", "PovratDobavljacu", "OdstupanjeOdobreno"]),
  rootCause: optionalText(2000),
  correctiveAction: optionalText(2000),
  actionOwnerEmployeeId: z.string().nullable(),
  dueDate: dateInput(),
  effectivenessCheckDate: dateInput(),
  workOrderId: z.string().nullable(),
  materialLotId: z.string().nullable(),
  supplierId: z.string().nullable(),
});
type FormIn = z.input<typeof schema>;
type FormOut = z.output<typeof schema>;

const empty: FormIn = {
  source: "Ostalo", description: "", containment: "", productDecision: "CekaOdluku", rootCause: "", correctiveAction: "",
  actionOwnerEmployeeId: null, dueDate: "", effectivenessCheckDate: "", workOrderId: null, materialLotId: null, supplierId: null,
};

const toValues = (n: NonConformity): FormIn => ({
  source: n.source, description: n.description, containment: n.containment ?? "", productDecision: n.productDecision,
  rootCause: n.rootCause ?? "", correctiveAction: n.correctiveAction ?? "", actionOwnerEmployeeId: n.actionOwnerEmployeeId,
  dueDate: n.dueDate ?? "", effectivenessCheckDate: n.effectivenessCheckDate ?? "", workOrderId: n.workOrderId,
  materialLotId: n.materialLotId, supplierId: n.supplierId,
});

/** Lots for the picker (newest first; NCs are rarely about old lots). */
function useLotChoices() {
  return useQuery({ queryKey: [...lotKey, "choices"], queryFn: () => api.get<Paged<Lot>>("/api/lots?pageSize=200&sort=-receivedDate"), staleTime: 60_000 });
}

/** DOMAIN §7.3: neighbour statuses; a blocked step shows why. */
function StatusPanel({ nc, transitions, dirty }: { nc: NonConformity; transitions: Transition<NcStatus>[]; dirty: boolean }) {
  const move = useNcStatus(nc.id);
  const order: NcStatus[] = ["Otvorena", "RadnjaUToku", "ProvjeraEfektivnosti", "Zatvorena"];
  return (
    <section className="space-y-2 rounded border border-slate-200 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="text-sm font-semibold">Status</h2>
        <Badge label={labelOf(ncStatus, nc.status)} />
      </div>
      <div className="flex flex-wrap gap-2">
        {transitions.map((t) => {
          const back = order.indexOf(t.to) < order.indexOf(nc.status);
          return (
            <div key={t.to} className="space-y-1">
              <button type="button" disabled={!t.allowed || dirty || move.isPending}
                onClick={() => move.mutate({ status: t.to, version: nc.version })}
                className={`min-h-11 rounded px-4 text-sm font-medium disabled:opacity-50 ${back ? "border border-slate-300" : "bg-blue-700 text-white"}`}>
                {back ? `← Vrati u „${ncStatus[t.to].text}”` : `→ ${ncStatus[t.to].text}`}
              </button>
              {!t.allowed && t.reason && <p className="max-w-xs text-xs text-slate-600">{t.reason}</p>}
            </div>
          );
        })}
      </div>
      {dirty && <p className="text-xs text-amber-800">Spremi izmjene prije promjene statusa.</p>}
      <ErrorBanner error={move.error} />
    </section>
  );
}

export function NcFormPage() {
  const { id } = useParams();
  const [params] = useSearchParams();
  const navigate = useNavigate();
  const me = useMe();
  const canEdit = hasRole(me.data, "Admin", "Kontrolor");
  const checkId = id ? null : params.get("kontrola");
  const detail = useNc(id);
  const prefill = useNcPrefill(checkId);
  const save = useSaveNc(id);
  const evidence = useNcEvidence(id ?? "");
  const employees = useEmployeeOptions();
  const suppliers = useSupplierOptions();
  const orders = useWorkOrderOptions();
  const lots = useLotChoices();

  const form = useForm<FormIn, unknown, FormOut>({
    resolver: zodResolver(schema),
    defaultValues: { ...empty, workOrderId: params.get("nalog"), materialLotId: params.get("lot") },
  });
  const { register, handleSubmit, control, setError, setValue, getValues, reset, formState: { errors, isDirty } } = form;
  const sync = useServerForm(form, detail.data?.nc, toValues);
  const nc = detail.data?.nc;
  const readOnly = !canEdit || nc?.status === "Zatvorena" || nc?.isVoided === true;
  const markSaved = useUnsavedChanges(!readOnly && isDirty && !save.isSuccess);

  // DOMAIN §7.1a: the offer from a failed check starts prefilled (only while nothing was typed yet).
  const p = prefill.data;
  useEffect(() => {
    if (p && !p.existingId && !isDirty)
      reset({ ...empty, source: p.source, description: p.description, workOrderId: p.workOrderId, materialLotId: p.materialLotId, supplierId: p.supplierId });
  }, [p]);

  const dueDate = useWatch({ control, name: "dueDate" });
  const onSubmit = handleSubmit((v) =>
    save.mutate({
      ...v, dueDate: v.dueDate ?? null, effectivenessCheckDate: v.effectivenessCheckDate ?? null,
      qualityCheckId: nc?.qualityCheckId ?? p?.qualityCheckId ?? null, version: sync.baseVersion,
    }, {
      onSuccess: (saved) => {
        markSaved();
        if (id) sync.markSaved(saved);
        else navigate(`/kvalitet/nc/${saved.id}`, { replace: true });
      },
      onError: (e) => {
        if (e instanceof ApiError) for (const [f, m] of Object.entries(e.fieldErrors)) setError(f as keyof FormIn, { message: m[0] ?? "" });
      },
    }),
  );

  if (id && detail.isPending) return <p className="p-4 text-slate-600">Učitavanje…</p>;
  if (id && detail.isError) return <div className="p-4"><ErrorBanner error={detail.error} /></div>;
  if (checkId && prefill.isPending) return <p className="p-4 text-slate-600">Učitavanje kontrole…</p>;
  if (checkId && prefill.isError) return <div className="p-4"><ErrorBanner error={prefill.error} /></div>;
  if (p?.existingId)
    return (
      <div className="space-y-3 p-4">
        <p>Za kontrolu {p.qualityCheckNumber} već postoji neusaglašenost {p.existingNumber}.</p>
        <Link to={`/kvalitet/nc/${p.existingId}`} className="inline-flex min-h-11 items-center rounded bg-blue-700 px-4 font-medium text-white">Otvori {p.existingNumber}</Link>
      </div>
    );

  const linkedCheck = nc?.qualityCheckId ? { id: nc.qualityCheckId, number: nc.qualityCheckNumber } : p ? { id: p.qualityCheckId, number: p.qualityCheckNumber } : null;

  return (
    <div className="max-w-4xl space-y-4 px-4 py-4">
      <Link to="/kvalitet/nc" className="text-sm text-blue-800 underline">← Neusaglašenosti</Link>
      <div className="flex flex-wrap items-center gap-3">
        <h2 className="text-lg font-semibold tabular-nums">{nc ? `NC ${nc.number}` : "Nova neusaglašenost"}</h2>
        {nc?.isVoided && <Badge label={{ text: "Poništena", tone: "zinc" }} />}
        {nc && !nc.isVoided && hasRole(me.data, "Admin") && <span className="ml-auto"><VoidNcButton nc={nc} /></span>}
      </div>
      {nc?.isVoided && <p className="rounded bg-slate-100 p-2 text-sm">Poništena: {nc.voidedReason}</p>}
      {linkedCheck && (
        <p className="text-sm">Iz kontrole <Link to={`/kvalitet/kontrole/${linkedCheck.id}`} className="text-blue-800 underline">{linkedCheck.number}</Link></p>
      )}
      {nc && !nc.isVoided && canEdit && <StatusPanel nc={nc} transitions={detail.data?.transitions ?? []} dirty={isDirty} />}
      {nc?.status === "Zatvorena" && <p className="rounded bg-slate-100 p-2 text-sm">NC je zatvorena i ne mijenja se. Za izmjenu je vrati u „Provjera efektivnosti”.</p>}

      <form onSubmit={onSubmit} noValidate className="space-y-4">
        <fieldset disabled={readOnly} className="space-y-4">
          <div className="grid gap-4 sm:grid-cols-2">
            <Field label="Izvor" htmlFor="nc-source" required>
              <select id="nc-source" className={inputClass} {...register("source")}>
                {Object.entries(ncSource).map(([v, l]) => <option key={v} value={v}>{l.text}</option>)}
              </select>
            </Field>
            <Field label="Odluka o proizvodu" htmlFor="nc-decision" required>
              <select id="nc-decision" className={inputClass} {...register("productDecision")}>
                {Object.entries(productDecision).map(([v, l]) => <option key={v} value={v}>{l.text}</option>)}
              </select>
            </Field>
          </div>
          <Field label="Opis neusaglašenosti" htmlFor="nc-desc" required error={errors.description?.message}>
            <textarea id="nc-desc" className={textareaClass} aria-invalid={!!errors.description} {...register("description")} />
          </Field>
          <Field label="Zadržavanje / trenutna korekcija" htmlFor="nc-cont" error={errors.containment?.message}>
            <textarea id="nc-cont" className={textareaClass} {...register("containment")} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Nalog" htmlFor="nc-wo">
              <Controller control={control} name="workOrderId" render={({ field }) => (
                <Combobox id="nc-wo" value={field.value} onChange={field.onChange} allowEmpty
                  options={(orders.data ?? []).map((o) => ({ value: o.id, label: o.number, detail: o.customer }))} />
              )} />
            </Field>
            <Field label="Lot" htmlFor="nc-lot" hint="Za izvor Dobavljač / Ulazni materijal (nije obavezno).">
              <Controller control={control} name="materialLotId" render={({ field }) => (
                <Combobox id="nc-lot" value={field.value} allowEmpty
                  onChange={(v) => {
                    field.onChange(v);
                    const lot = lots.data?.items.find((l) => l.id === v);
                    if (lot && !getValues("supplierId")) setValue("supplierId", lot.supplierId, { shouldDirty: true });
                  }}
                  options={(lots.data?.items ?? []).map((l) => ({ value: l.id, label: l.lotNumber, detail: `${l.itemName} · ${l.supplierName}` }))} />
              )} />
            </Field>
            <Field label="Dobavljač" htmlFor="nc-supplier">
              <Controller control={control} name="supplierId" render={({ field }) => (
                <Combobox id="nc-supplier" value={field.value} onChange={field.onChange} allowEmpty
                  options={(suppliers.data ?? []).map((s) => ({ value: s.id, label: s.name }))} />
              )} />
            </Field>
          </div>
          <Field label="Analiza uzroka" htmlFor="nc-cause" error={errors.rootCause?.message}>
            <textarea id="nc-cause" className={textareaClass} {...register("rootCause")} />
          </Field>
          <Field label="Korektivna radnja" htmlFor="nc-action" error={errors.correctiveAction?.message}>
            <textarea id="nc-action" className={textareaClass} {...register("correctiveAction")} />
          </Field>
          <div className="grid gap-4 sm:grid-cols-3">
            <Field label="Vlasnik radnje" htmlFor="nc-owner">
              <Controller control={control} name="actionOwnerEmployeeId" render={({ field }) => (
                <Combobox id="nc-owner" value={field.value} onChange={field.onChange} allowEmpty
                  options={(employees.data ?? []).map((e) => ({ value: e.id, label: e.fullName }))} />
              )} />
            </Field>
            <Field label="Rok" htmlFor="nc-due" error={errors.dueDate?.message}
              hint={dueDate ? undefined : "NC bez roka se ne prati na dashboardu"}>
              <input id="nc-due" type="date" className={inputClass} {...register("dueDate")} />
            </Field>
            <Field label="Datum provjere efektivnosti" htmlFor="nc-eff" error={errors.effectivenessCheckDate?.message}>
              <input id="nc-eff" type="date" className={inputClass} {...register("effectivenessCheckDate")} />
            </Field>
          </div>
        </fieldset>

        <ChangedOnServerNotice show={sync.changedOnServer} onDiscard={sync.discard} />
        <ErrorBanner error={save.error instanceof ApiError && Object.keys(save.error.fieldErrors).length ? null : save.error}
          onRefresh={() => { save.reset(); void sync.reload(async () => ({ data: (await detail.refetch()).data?.nc })); }} />
        {!readOnly && (
          <div className="flex gap-2">
            <button type="submit" disabled={save.isPending} className="min-h-12 rounded bg-blue-700 px-5 font-medium text-white disabled:opacity-60">
              {save.isPending ? "Spremanje…" : id ? "Spremi" : "Otvori NC"}
            </button>
            <button type="button" onClick={() => navigate(-1)} className="min-h-12 rounded border border-slate-300 px-5">Odustani</button>
          </div>
        )}
      </form>

      {nc && (
        <>
          <EvidenceFiles title="Dokazi / zapisi" files={detail.data?.evidence ?? []} add={evidence.add} remove={evidence.remove}
            canAdd={canEdit && !nc.isVoided} canRemove={hasRole(me.data, "Admin")} />
          <section>
            <h2 className="text-sm font-semibold">Istorija</h2>
            <HistoryList scope="non-conformity" id={nc.id} />
          </section>
        </>
      )}
    </div>
  );
}
