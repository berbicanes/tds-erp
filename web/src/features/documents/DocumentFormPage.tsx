import { useState, type ReactNode } from "react";
import { Link, useNavigate, useParams } from "react-router";
import { Controller, useForm } from "react-hook-form";
import { zodResolver } from "@hookform/resolvers/zod";
import { z } from "zod";
import { Badge } from "../../components/Badge";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { ErrorBanner } from "../../components/ErrorBanner";
import { HistoryList } from "../../components/HistoryList";
import { Combobox } from "../../components/form/Combobox";
import { Field, inputClass, textareaClass } from "../../components/form/Field";
import { FilePicker } from "../../components/form/FilePicker";
import { ChangedOnServerNotice, useUnsavedChanges } from "../../components/UnsavedChanges";
import { useServerForm } from "../../hooks/useServerForm";
import { ApiError } from "../../lib/api";
import { hasRole, useMe } from "../../lib/auth";
import { todaySarajevo } from "../../lib/date";
import { fileUrl, uploadFile } from "../../lib/files";
import { EMPTY, formatDate } from "../../lib/format";
import { documentKind, documentStatus, labelOf, type DocumentStatus } from "../../lib/labels";
import { dateInput, optionalText, requiredText } from "../../lib/schema";
import { useEmployeeOptions } from "../settings/api";
import {
  nextVersionLabel, useDocument, useDocumentStatus, useNewVersion, useNextReview, useSaveDocument, useVoidDocument,
  type ControlledDocument, type DocumentDetail,
} from "./api";

// Mirrors SaveDocumentValidator.
const schema = z.object({
  title: requiredText(200, "Unesi naziv dokumenta."),
  code: requiredText(40, "Unesi oznaku dokumenta, npr. PR-01."),
  kind: z.enum(["Procedura", "RadnoUputstvo", "ObrazacZapis", "EksterniDokument"]),
  docVersion: requiredText(10, "Unesi verziju, npr. 01."),
  ownerEmployeeId: z.string().nullable(),
  approvalDate: dateInput(),
  effectiveDate: dateInput(),
  nextReviewDate: dateInput(),
  changeDescription: optionalText(1000),
});
type FormIn = z.input<typeof schema>;
type FormOut = z.output<typeof schema>;

const toValues = (d: ControlledDocument): FormIn => ({
  title: d.title, code: d.code, kind: d.kind, docVersion: d.docVersion, ownerEmployeeId: d.ownerEmployeeId,
  approvalDate: d.approvalDate ?? "", effectiveDate: d.effectiveDate ?? "", nextReviewDate: d.nextReviewDate ?? "", changeDescription: d.changeDescription ?? "",
});

function Info({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className="text-sm">{children}</dd>
    </div>
  );
}

/** DOMAIN §7.4 transitions; approving asks for the date and says the previous approved version is withdrawn. */
function StatusActions({ detail, dirty }: { detail: DocumentDetail; dirty: boolean }) {
  const d = detail.document;
  const move = useDocumentStatus(d.id);
  const [approving, setApproving] = useState(false);
  const [date, setDate] = useState(d.approvalDate ?? todaySarajevo());
  const previous = detail.versions.find((v) => v.status === "Odobren" && !v.isVoided && v.id !== d.id);
  const text: Record<DocumentStatus, string> = { Nacrt: "← Vrati u nacrt", NaPregledu: "→ Pošalji na pregled", Odobren: "Odobri", Povucen: "Povuci" };

  return (
    <section className="space-y-2 rounded border border-slate-200 p-3">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="text-sm font-semibold">Status</h2>
        <Badge label={labelOf(documentStatus, d.status)} />
        {d.status === "Povucen" && <span className="text-xs text-slate-600">Povučen dokument je konačan. Za izmjenu napravi novu verziju.</span>}
      </div>
      <div className="flex flex-wrap gap-2">
        {detail.transitions.map((t) => (
          <div key={t.to} className="space-y-1">
            <button type="button" disabled={dirty || move.isPending || (!t.allowed && t.to !== "Odobren")}
              onClick={() => (t.to === "Odobren" ? setApproving(true) : move.mutate({ status: t.to, version: d.version }))}
              className={`min-h-11 rounded px-4 text-sm font-medium disabled:opacity-50 ${t.to === "Nacrt" || t.to === "Povucen" ? "border border-slate-300" : "bg-blue-700 text-white"}`}>
              {text[t.to]}
            </button>
            {!t.allowed && t.reason && t.to !== "Odobren" && <p className="max-w-xs text-xs text-slate-600">{t.reason}</p>}
          </div>
        ))}
      </div>
      {dirty && <p className="text-xs text-amber-800">Spremi izmjene prije promjene statusa.</p>}
      <ErrorBanner error={move.error} />
      <ConfirmDialog open={approving} title={`Odobriti ${d.code} v${d.docVersion}?`} confirmText="Odobri" onCancel={() => setApproving(false)}
        onConfirm={() => { setApproving(false); move.mutate({ status: "Odobren", approvalDate: date, version: d.version }); }}>
        <label htmlFor="doc-approve-date" className="block text-sm font-medium">Datum odobrenja *</label>
        <input id="doc-approve-date" type="date" max={todaySarajevo()} className={inputClass} value={date} onChange={(e) => setDate(e.target.value)} />
        {!d.fileId && <p className="mt-2 text-sm text-red-700">Dokument nema priložen fajl; odobravanje će biti odbijeno.</p>}
        {previous && <p className="mt-2 text-sm">Važeća verzija v{previous.docVersion} biće automatski povučena.</p>}
      </ConfirmDialog>
    </section>
  );
}

function NextReviewEditor({ d }: { d: ControlledDocument }) {
  const set = useNextReview(d.id);
  const [value, setValue] = useState(d.nextReviewDate ?? "");
  const changed = value !== (d.nextReviewDate ?? "");
  useUnsavedChanges(changed && !set.isPending);
  return (
    <div className="flex flex-wrap items-end gap-2">
      <Field label="Sljedeća provjera" htmlFor="doc-review" hint="Odobren dokument je zaključan; mijenja se samo ovaj datum.">
        <input id="doc-review" type="date" className={inputClass} value={value} onChange={(e) => setValue(e.target.value)} />
      </Field>
      <button type="button" disabled={!changed || set.isPending} onClick={() => set.mutate({ nextReviewDate: value || null, version: d.version })}
        className="min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white disabled:opacity-50">Spremi datum</button>
      <ErrorBanner error={set.error} />
    </div>
  );
}

function NewVersionButton({ d }: { d: ControlledDocument }) {
  const create = useNewVersion(d.id);
  const navigate = useNavigate();
  const [open, setOpen] = useState(false);
  const [label, setLabel] = useState(nextVersionLabel(d.docVersion));
  return (
    <>
      <button type="button" onClick={() => setOpen(true)} className="min-h-10 rounded border border-slate-300 px-3 text-sm">Nova verzija</button>
      <ConfirmDialog open={open} title={`Nova verzija dokumenta ${d.code}`} confirmText="Napravi" onCancel={() => setOpen(false)}
        onConfirm={() => { setOpen(false); create.mutate(label.trim(), { onSuccess: (n) => navigate(`/dokumenti/${n.id}`) }); }}>
        <label htmlFor="doc-new-version" className="block text-sm font-medium">Oznaka verzije *</label>
        <input id="doc-new-version" className={inputClass} value={label} maxLength={10} onChange={(e) => setLabel(e.target.value)} />
        <p className="mt-2 text-xs text-slate-600">Podaci se kopiraju, nova verzija počinje kao nacrt. Važeća verzija ostaje dok se nova ne odobri.</p>
      </ConfirmDialog>
      <ErrorBanner error={create.error} />
    </>
  );
}

function VoidDocumentButton({ d }: { d: ControlledDocument }) {
  const voidDoc = useVoidDocument();
  const [open, setOpen] = useState(false);
  const [reason, setReason] = useState("");
  return (
    <>
      <button type="button" onClick={() => setOpen(true)} className="min-h-10 rounded border border-slate-300 px-3 text-sm">Poništi</button>
      <ConfirmDialog open={open} title={`Poništiti ${d.code} v${d.docVersion}?`} confirmText="Poništi" danger onCancel={() => setOpen(false)}
        onConfirm={() => { if (reason.trim()) { setOpen(false); voidDoc.mutate({ id: d.id, reason: reason.trim(), version: d.version }); } }}>
        <label htmlFor="doc-void-reason" className="block text-sm font-medium">Razlog *</label>
        <input id="doc-void-reason" className={inputClass} value={reason} maxLength={300} onChange={(e) => setReason(e.target.value)} />
      </ConfirmDialog>
      <ErrorBanner error={voidDoc.error} />
    </>
  );
}

export function DocumentFormPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const me = useMe();
  const canEdit = hasRole(me.data, "Admin", "Kontrolor");
  const detail = useDocument(id);
  const save = useSaveDocument(id);
  const employees = useEmployeeOptions();
  const [file, setFile] = useState<File | null>(null);
  const [uploadError, setUploadError] = useState<unknown>(null);

  const form = useForm<FormIn, unknown, FormOut>({
    resolver: zodResolver(schema),
    defaultValues: { title: "", code: "", kind: "Procedura", docVersion: "01", ownerEmployeeId: null, approvalDate: "", effectiveDate: "", nextReviewDate: "", changeDescription: "" },
  });
  const { register, handleSubmit, control, setError, formState: { errors, isDirty } } = form;
  const d = detail.data?.document;
  const sync = useServerForm(form, d, toValues);
  const editable = canEdit && (!d || (!d.isVoided && (d.status === "Nacrt" || d.status === "NaPregledu")));
  const markSaved = useUnsavedChanges(editable && (isDirty || file !== null) && !save.isSuccess);

  const onSubmit = handleSubmit(async (v) => {
    setUploadError(null);
    let fileId = d?.fileId ?? null;
    try {
      if (file) fileId = (await uploadFile(file)).id;
    } catch (e) {
      setUploadError(e);
      return;
    }
    save.mutate({ ...v, approvalDate: v.approvalDate ?? null, effectiveDate: v.effectiveDate ?? null, nextReviewDate: v.nextReviewDate ?? null, fileId, version: sync.baseVersion }, {
      onSuccess: (saved) => {
        markSaved();
        setFile(null);
        if (id) sync.markSaved(saved);
        else navigate(`/dokumenti/${saved.id}`, { replace: true });
      },
      onError: (e) => {
        if (e instanceof ApiError) for (const [f, m] of Object.entries(e.fieldErrors)) setError(f as keyof FormIn, { message: m[0] ?? "" });
      },
    });
  });

  if (id && detail.isPending) return <p className="p-4 text-slate-600">Učitavanje…</p>;
  if (id && detail.isError) return <div className="p-4"><ErrorBanner error={detail.error} /></div>;

  return (
    <div className="max-w-4xl space-y-4 px-4 py-4">
      <Link to="/dokumenti" className="text-sm text-blue-800 underline">← Dokumenti</Link>
      <div className="flex flex-wrap items-center gap-3">
        <h1 className="text-lg font-semibold">{d ? `${d.code} v${d.docVersion}` : "Novi dokument"}</h1>
        {d?.isVoided && <Badge label={{ text: "Poništen", tone: "zinc" }} />}
        {d && !d.isVoided && (
          <span className="ml-auto flex flex-wrap gap-2">
            {canEdit && <NewVersionButton d={d} />}
            {hasRole(me.data, "Admin") && <VoidDocumentButton d={d} />}
          </span>
        )}
      </div>
      {d?.isVoided && <p className="rounded bg-slate-100 p-2 text-sm">Poništen: {d.voidedReason}</p>}
      {d && !d.isVoided && canEdit && detail.data && <StatusActions detail={detail.data} dirty={isDirty || file !== null} />}

      {d && !editable ? (
        <>
          <dl className="grid grid-cols-2 gap-4 sm:grid-cols-4">
            <div className="col-span-2"><Info label="Naziv">{d.title}</Info></div>
            <Info label="Vrsta">{labelOf(documentKind, d.kind).text}</Info>
            <Info label="Vlasnik">{d.ownerName ?? EMPTY}</Info>
            <Info label="Datum odobrenja">{formatDate(d.approvalDate)}</Info>
            <Info label="Datum primjene">{formatDate(d.effectiveDate)}</Info>
            <Info label="Sljedeća provjera">{formatDate(d.nextReviewDate)}</Info>
            <Info label="Važeći dokument">{d.fileId ? <a href={fileUrl(d.fileId)} target="_blank" rel="noreferrer" className="text-blue-800 underline">{d.fileName}</a> : "Nema"}</Info>
            <div className="col-span-2 sm:col-span-4"><Info label="Opis izmjene">{d.changeDescription ?? EMPTY}</Info></div>
          </dl>
          {canEdit && d.status === "Odobren" && !d.isVoided && <NextReviewEditor key={d.version} d={d} />}
        </>
      ) : (
        <form onSubmit={onSubmit} noValidate className="space-y-4">
          <fieldset disabled={!editable} className="space-y-4">
            <div className="grid gap-4 sm:grid-cols-3">
              <Field label="Oznaka dokumenta" htmlFor="doc-code" required error={errors.code?.message} hint="npr. PR-01">
                <input id="doc-code" className={inputClass} aria-invalid={!!errors.code} {...register("code")} />
              </Field>
              <Field label="Verzija" htmlFor="doc-version" required error={errors.docVersion?.message}>
                <input id="doc-version" className={inputClass} aria-invalid={!!errors.docVersion} {...register("docVersion")} />
              </Field>
              <Field label="Vrsta" htmlFor="doc-kind" required>
                <select id="doc-kind" className={inputClass} {...register("kind")}>
                  {Object.entries(documentKind).map(([v, l]) => <option key={v} value={v}>{l.text}</option>)}
                </select>
              </Field>
            </div>
            <Field label="Naziv dokumenta" htmlFor="doc-title" required error={errors.title?.message}>
              <input id="doc-title" className={inputClass} aria-invalid={!!errors.title} {...register("title")} />
            </Field>
            <div className="grid gap-4 sm:grid-cols-2">
              <Field label="Vlasnik dokumenta" htmlFor="doc-owner">
                <Controller control={control} name="ownerEmployeeId" render={({ field }) => (
                  <Combobox id="doc-owner" value={field.value} onChange={field.onChange} allowEmpty
                    options={(employees.data ?? []).map((e) => ({ value: e.id, label: e.fullName }))} />
                )} />
              </Field>
              <Field label="Datum odobrenja" htmlFor="doc-approval" error={errors.approvalDate?.message} hint="Obavezno pri odobravanju; ne u budućnosti.">
                <input id="doc-approval" type="date" max={todaySarajevo()} className={inputClass} {...register("approvalDate")} />
              </Field>
              <Field label="Datum primjene" htmlFor="doc-effective" error={errors.effectiveDate?.message}>
                <input id="doc-effective" type="date" className={inputClass} {...register("effectiveDate")} />
              </Field>
              <Field label="Sljedeća provjera" htmlFor="doc-review-edit" error={errors.nextReviewDate?.message}>
                <input id="doc-review-edit" type="date" className={inputClass} {...register("nextReviewDate")} />
              </Field>
            </div>
            <Field label="Opis izmjene" htmlFor="doc-change" error={errors.changeDescription?.message}>
              <textarea id="doc-change" className={textareaClass} {...register("changeDescription")} />
            </Field>
            <Field label="Važeći dokument (fajl)" htmlFor="doc-file" hint="Obavezno pri odobravanju.">
              <div className="space-y-2">
                {d?.fileId && !file && (
                  <p className="text-sm">Trenutni: <a href={fileUrl(d.fileId)} target="_blank" rel="noreferrer" className="text-blue-800 underline">{d.fileName}</a></p>
                )}
                <FilePicker file={file} onChange={setFile} label={d?.fileId ? "Zamijeni fajl" : "Odaberi fajl"} />
              </div>
            </Field>
          </fieldset>
          <ChangedOnServerNotice show={sync.changedOnServer} onDiscard={sync.discard} />
          <ErrorBanner error={uploadError ?? (save.error instanceof ApiError && Object.keys(save.error.fieldErrors).length ? null : save.error)}
            onRefresh={() => { save.reset(); void sync.reload(async () => ({ data: (await detail.refetch()).data?.document })); }} />
          {editable && (
            <div className="flex gap-2">
              <button type="submit" disabled={save.isPending} className="min-h-12 rounded bg-blue-700 px-5 font-medium text-white disabled:opacity-60">
                {save.isPending ? "Spremanje…" : "Spremi"}
              </button>
              <button type="button" onClick={() => navigate("/dokumenti")} className="min-h-12 rounded border border-slate-300 px-5">Odustani</button>
            </div>
          )}
        </form>
      )}

      {d && detail.data && (
        <>
          <section className="space-y-2">
            <h2 className="text-sm font-semibold">Verzije</h2>
            <ul className="divide-y divide-slate-100 rounded border border-slate-200 text-sm">
              {detail.data.versions.map((v) => (
                <li key={v.id} className={`flex flex-wrap items-center gap-3 px-3 py-2 ${v.isVoided ? "text-slate-400" : ""}`}>
                  {v.id === d.id ? <strong>v{v.docVersion}</strong> : <Link to={`/dokumenti/${v.id}`} className="text-blue-800 underline">v{v.docVersion}</Link>}
                  {v.isVoided ? <span>Poništena</span> : <Badge label={labelOf(documentStatus, v.status)} />}
                  <span className="text-slate-600">{v.approvalDate ? `odobren ${formatDate(v.approvalDate)}` : ""}</span>
                  <span className="text-slate-600">{v.changeDescription ?? ""}</span>
                </li>
              ))}
            </ul>
          </section>
          <section>
            <h2 className="text-sm font-semibold">Istorija</h2>
            <HistoryList scope="document" id={d.id} />
          </section>
        </>
      )}
    </div>
  );
}
