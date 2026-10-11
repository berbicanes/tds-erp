import { useState } from "react";
import { Link } from "react-router";
import { ErrorBanner } from "../../components/ErrorBanner";
import { hasRole, useMe } from "../../lib/auth";
import type { WorkOrder } from "../workOrders/api";
import { useWorkOrderChecks, useWorkOrderNcs } from "./api";
import { CheckForm } from "./CheckForm";
import { CheckTable } from "./CheckTable";
import { NcTable } from "./NcTable";

/** UI §6 Kvalitet: checks by phase and the NCs of the order. */
export function WorkOrderQualityTab({ wo }: { wo: WorkOrder }) {
  const checks = useWorkOrderChecks(wo.id);
  const ncs = useWorkOrderNcs(wo.id);
  const me = useMe();
  const [adding, setAdding] = useState(false);
  const canCreate = hasRole(me.data, "Admin", "Kontrolor");

  return (
    <div className="space-y-4 pt-3">
      <section className="space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="font-semibold">Kontrole</h2>
          {canCreate && !adding && (
            <button type="button" onClick={() => setAdding(true)} className="ml-auto min-h-11 rounded bg-blue-700 px-4 text-sm font-medium text-white">Dodaj kontrolu</button>
          )}
        </div>
        {adding && <CheckForm owner={{ workOrderId: wo.id }} onDone={() => setAdding(false)} />}
        <ErrorBanner error={checks.error} />
        <CheckTable checks={checks.data} emptyText="Nema kontrola na ovom nalogu." />
      </section>
      <section className="space-y-3">
        <div className="flex flex-wrap items-center gap-2">
          <h2 className="font-semibold">Neusaglašenosti</h2>
          {canCreate && (
            <Link to={`/kvalitet/nc/nova?nalog=${wo.id}`} className="ml-auto flex min-h-11 items-center rounded border border-slate-300 px-4 text-sm">Nova NC</Link>
          )}
        </div>
        <ErrorBanner error={ncs.error} />
        <NcTable ncs={ncs.data} emptyText="Nema neusaglašenosti na ovom nalogu." />
      </section>
    </div>
  );
}
