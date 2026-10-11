import { useState } from "react";
import { Link } from "react-router";
import { ErrorBanner } from "../../components/ErrorBanner";
import { hasRole, useMe } from "../../lib/auth";
import { useLotChecks, useLotNcs } from "./api";
import { CheckForm } from "./CheckForm";
import { CheckTable } from "./CheckTable";
import { NcTable } from "./NcTable";

/** Lot detail: incoming material checks (DATA_MODEL §10: bound to the lot) and NCs about the lot. */
export function LotQualitySection({ lotId, isVoided }: { lotId: string; isVoided: boolean }) {
  const checks = useLotChecks(lotId);
  const ncs = useLotNcs(lotId);
  const me = useMe();
  const canCreate = hasRole(me.data, "Admin", "Kontrolor") && !isVoided;
  const [adding, setAdding] = useState(false);

  return (
    <section className="space-y-3">
      <div className="flex flex-wrap items-center gap-2">
        <h2 className="font-semibold">Ulazna kontrola</h2>
        {canCreate && !adding && (
          <button type="button" onClick={() => setAdding(true)} className="ml-auto min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white">Dodaj ulaznu kontrolu</button>
        )}
      </div>
      {adding && <CheckForm owner={{ materialLotId: lotId }} onDone={() => setAdding(false)} />}
      <ErrorBanner error={checks.error ?? ncs.error} />
      <CheckTable checks={checks.data} emptyText="Nema ulaznih kontrola za ovaj lot." />
      <div className="flex flex-wrap items-center gap-2 pt-2">
        <h2 className="font-semibold">Neusaglašenosti</h2>
        {canCreate && <Link to={`/kvalitet/nc/nova?lot=${lotId}`} className="ml-auto flex min-h-11 items-center rounded border border-slate-300 px-4 text-sm">Nova NC</Link>}
      </div>
      <NcTable ncs={ncs.data} emptyText="Nema neusaglašenosti za ovaj lot." />
    </section>
  );
}
