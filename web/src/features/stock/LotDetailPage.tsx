import { useState } from "react";
import { useLocation, useNavigate, useParams } from "react-router";
import { Badge } from "../../components/Badge";
import { ErrorBanner, WarningList } from "../../components/ErrorBanner";
import { Field, inputClass } from "../../components/form/Field";
import { Combobox } from "../../components/form/Combobox";
import { ConfirmDialog } from "../../components/ConfirmDialog";
import { hasRole, useMe } from "../../lib/auth";
import { labelOf, receiptDecision, type ReceiptDecision } from "../../lib/labels";
import { EMPTY, formatDate } from "../../lib/format";
import { itemTitle, qtyWithUnit } from "../../lib/material";
import { useEmployeeOptions } from "../settings/api";
import { allowedDecisions, useDecideLot, useLot, useVoidLot, type Lot } from "./lotsApi";
import { LotEditForm } from "./LotEditForm";
import { LotMovements } from "./LotMovements";
import { useUnsavedChanges } from "../../components/UnsavedChanges";
import { FilePicker } from "../../components/form/FilePicker";
import { fileUrl, uploadFile } from "../../lib/files";
import { useMutation, useQueryClient } from "@tanstack/react-query";
import { api, type WithWarnings } from "../../lib/api";
import { lotKey } from "./lotsApi";
import { HistoryList } from "../../components/HistoryList";
import { LotQualitySection } from "../quality/LotQualitySection";

function Info({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div>
      <dt className="text-xs text-slate-500">{label}</dt>
      <dd className="text-sm">{children}</dd>
    </div>
  );
}

function DecisionPanel({ lot, onWarnings }: { lot: Lot; onWarnings: (w: string[]) => void }) {
  const decide = useDecideLot(lot.id);
  const employees = useEmployeeOptions();
  const options = allowedDecisions[lot.receiptDecision];
  const [decision, setDecision] = useState<ReceiptDecision | "">("");
  const [inspector, setInspector] = useState<string | null>(lot.inspectedByEmployeeId);
  const [reason, setReason] = useState("");
  const [confirm, setConfirm] = useState(false);
  const [localError, setLocalError] = useState<string | null>(null);
  useUnsavedChanges(decision !== "" || reason.trim() !== "");

  if (options.length === 0) return <p className="text-sm text-slate-600">Lot je odbijen. Odluka se više ne može mijenjati.</p>;

  const submit = () => {
    setConfirm(false);
    decide.mutate(
      { decision: decision as ReceiptDecision, inspectedByEmployeeId: inspector, reason: reason.trim() || null, version: lot.version },
      { onSuccess: (r) => { onWarnings(r.warnings); setDecision(""); setReason(""); } },
    );
  };

  const validateAndSubmit = () => {
    if (!decision) return setLocalError("Odaberi novu odluku.");
    if (!inspector) return setLocalError("Odaberi ko je kontrolisao lot.");
    if (decision === "Odbijen" && !reason.trim()) return setLocalError("Razlog odbijanja je obavezan.");
    setLocalError(null);
    if (decision === "Odbijen") setConfirm(true);
    else submit();
  };

  return (
    <div className="space-y-3 rounded border border-slate-200 p-3">
      <h3 className="text-sm font-semibold">Promijeni odluku prijema</h3>
      <div className="flex flex-wrap gap-2">
        {options.map((d) => (
          <button key={d} type="button" aria-pressed={decision === d} onClick={() => setDecision(d)}
            className={`min-h-11 rounded border px-3 text-sm ${decision === d ? "border-blue-700 bg-blue-50 font-semibold" : "border-slate-300"}`}>
            {labelOf(receiptDecision, d).text}
          </button>
        ))}
      </div>
      <Field label="Kontrolisao" htmlFor="inspector" required>
        <Combobox id="inspector" value={inspector} onChange={setInspector}
          options={(employees.data ?? []).map((e) => ({ value: e.id, label: e.fullName }))} />
      </Field>
      {decision === "Odbijen" && (
        <Field label="Razlog odbijanja" htmlFor="reason" required hint="Cijelo preostalo stanje lota se automatski otpisuje.">
          <input id="reason" className={inputClass} value={reason} onChange={(e) => setReason(e.target.value)} maxLength={300} />
        </Field>
      )}
      {localError && <p className="text-sm text-red-700">{localError}</p>}
      <ErrorBanner error={decide.error} />
      <button type="button" onClick={validateAndSubmit} disabled={decide.isPending} className="min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white disabled:opacity-60">
        Spremi odluku
      </button>
      <ConfirmDialog open={confirm} title={`Odbiti lot ${lot.lotNumber}?`} confirmText="Odbij i otpiši" danger onConfirm={submit} onCancel={() => setConfirm(false)}>
        Otpisaće se {qtyWithUnit(lot.onHand, lot.unit)}. Odbijen lot se više ne može vratiti.
      </ConfirmDialog>
    </div>
  );
}

function CertificatePanel({ lot, onWarnings }: { lot: Lot; onWarnings: (w: string[]) => void }) {
  const qc = useQueryClient();
  const [file, setFile] = useState<File | null>(null);
  useUnsavedChanges(file !== null);
  const save = useMutation({
    mutationFn: async (f: File) => {
      const uploaded = await uploadFile(f);
      return api.post<WithWarnings<Lot>>(`/api/lots/${lot.id}/certificate`, { fileId: uploaded.id, version: lot.version });
    },
    onSuccess: (r) => {
      setFile(null);
      onWarnings(r.warnings);
      void qc.invalidateQueries({ queryKey: lotKey });
    },
  });
  return (
    <div className="space-y-2 rounded border border-slate-200 p-3">
      <h3 className="text-sm font-semibold">{lot.certificateFileId ? "Zamijeni certifikat materijala" : "Certifikat materijala"}</h3>
      <FilePicker file={file} onChange={setFile} label="Odaberi certifikat (PDF ili slika)" />
      <ErrorBanner error={save.error} />
      {file && (
        <button type="button" disabled={save.isPending} onClick={() => save.mutate(file)} className="min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white disabled:opacity-60">
          {save.isPending ? "Slanje…" : "Spremi certifikat"}
        </button>
      )}
    </div>
  );
}

function VoidPanel({ lot }: { lot: Lot }) {
  const voidLot = useVoidLot(lot.id);
  const [reason, setReason] = useState("");
  const [open, setOpen] = useState(false);
  return (
    <div className="space-y-2">
      <button type="button" onClick={() => setOpen(true)} className="min-h-11 rounded border border-red-300 px-4 text-sm text-red-800">Poništi lot</button>
      <ErrorBanner error={voidLot.error} />
      <ConfirmDialog open={open} title={`Poništiti lot ${lot.lotNumber}?`} confirmText="Poništi" danger
        onCancel={() => setOpen(false)}
        onConfirm={() => { if (reason.trim()) { setOpen(false); voidLot.mutate({ reason: reason.trim(), version: lot.version }); } }}>
        <p className="mb-2">Poništava se i prijem. Moguće samo ako lot nema drugih pokreta i nije vezan za nalog.</p>
        <label htmlFor="void-reason" className="block text-sm font-medium">Razlog *</label>
        <input id="void-reason" className={inputClass} value={reason} onChange={(e) => setReason(e.target.value)} maxLength={300} />
      </ConfirmDialog>
    </div>
  );
}

export function LotDetailPage() {
  const { id } = useParams();
  const navigate = useNavigate();
  const location = useLocation();
  const me = useMe();
  const lot = useLot(id);
  const [warnings, setWarnings] = useState<string[]>((location.state as { warnings?: string[] } | null)?.warnings ?? []);
  const [editing, setEditing] = useState(false);
  const canEdit = hasRole(me.data, "Admin", "Kontrolor");

  if (lot.isPending) return <p className="p-4 text-slate-600">Učitavanje…</p>;
  if (lot.isError) return <div className="p-4"><ErrorBanner error={lot.error} /></div>;
  const l = lot.data;

  return (
    <div className="space-y-4 px-4 py-4">
      <div className="flex flex-wrap items-center gap-3">
        <button type="button" onClick={() => navigate("/zalihe/lotovi")} className="text-sm text-blue-800 underline">← Lotovi</button>
        <h2 className="text-lg font-semibold">Lot {l.lotNumber}</h2>
        <Badge label={labelOf(receiptDecision, l.receiptDecision)} />
        {l.isVoided && <Badge label={{ text: "Poništen", tone: "zinc" }} />}
      </div>
      {l.isVoided && <p className="rounded bg-zinc-100 p-3 text-sm">Poništen: {l.voidedReason}</p>}
      <WarningList warnings={warnings} />

      {editing ? (
        <LotEditForm lot={l} onDone={() => setEditing(false)} />
      ) : (
        <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
          <Info label="Artikal">{itemTitle({ name: l.itemName, grade: l.itemGrade, thicknessMm: l.itemThicknessMm })}</Info>
          <Info label="Dobavljač">{l.supplierName}</Info>
          <Info label="Datum prijema">{formatDate(l.receivedDate)}</Info>
          <Info label="Primljeno">{qtyWithUnit(l.receivedQty, l.unit)}</Info>
          <Info label="Na stanju"><span className={l.onHand < 0 ? "text-red-700" : ""}>{qtyWithUnit(l.onHand, l.unit)}</span></Info>
          <Info label="Dimenzije / količina">{l.dimensionsDescription ?? EMPTY}</Info>
          <Info label="Skladišna lokacija">{l.storageLocation ?? EMPTY}</Info>
          <Info label="Kontrolisao">{l.inspectedByName ?? EMPTY}</Info>
          <Info label="Certifikat">
            {l.certificateFileId ? (
              <a href={fileUrl(l.certificateFileId)} target="_blank" rel="noreferrer" className="text-blue-800 underline">{l.certificateFileName}</a>
            ) : "Nema"}
          </Info>
        </dl>
      )}

      {canEdit && !l.isVoided && !editing && (
        <div className="flex flex-wrap gap-2">
          <button type="button" onClick={() => setEditing(true)} className="min-h-11 rounded border border-slate-300 px-4 text-sm">Uredi podatke</button>
          {hasRole(me.data, "Admin") && <VoidPanel lot={l} />}
        </div>
      )}

      {canEdit && !l.isVoided && (
        <div className="grid gap-4 lg:grid-cols-2">
          <DecisionPanel lot={l} onWarnings={setWarnings} />
          <CertificatePanel lot={l} onWarnings={setWarnings} />
        </div>
      )}

      <LotMovements lot={l} />
      <LotQualitySection lotId={l.id} isVoided={l.isVoided} />
      <section>
        <h2 className="font-semibold">Istorija</h2>
        <p className="text-xs text-slate-600">Lot, njegove kontrole, neusaglašenosti, pokreti zalihe i rezervacije za naloge.</p>
        <HistoryList scope="lot" id={l.id} />
      </section>
    </div>
  );
}
