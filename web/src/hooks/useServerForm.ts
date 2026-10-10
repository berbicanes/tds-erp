import { useCallback, useEffect, useRef, useState } from "react";
import type { FieldValues, UseFormReturn } from "react-hook-form";
import { formSync, initialFormSync, type FormSyncState, type Versioned } from "../lib/formSync";

/**
 * Binds an edit form to server data without ever overwriting unsaved input (see lib/formSync.ts).
 * - `baseVersion`: send this on save (the version the user started editing).
 * - `changedOnServer`: newer data exists; the user should save (→ 409 + "Osvježi") or discard.
 * - `markSaved(data)`: call after a successful save that stays on the page.
 * - `discard()`: drop unsaved changes and load the newest server data already received.
 * - `reload(refetch)`: fetch the newest data and load it, dropping unsaved changes ("Osvježi" after a 409).
 */
export function useServerForm<TData extends Versioned, TValues extends FieldValues, TOut = TValues>(
  form: UseFormReturn<TValues, unknown, TOut>,
  data: TData | undefined,
  toValues: (data: TData) => TValues,
) {
  const sync = useRef<FormSyncState<TData>>(initialFormSync<TData>());
  const [, rerender] = useState(0);
  const toValuesRef = useRef(toValues);
  toValuesRef.current = toValues;
  const { reset, formState } = form;
  const isDirty = formState.isDirty;

  const apply = useCallback(
    (action: Parameters<typeof formSync<TData>>[1]) => {
      const r = formSync(sync.current, action);
      sync.current = r.state;
      if (r.resetTo) reset(toValuesRef.current(r.resetTo));
      rerender((n) => n + 1);
    },
    [reset],
  );

  useEffect(() => {
    if (data) apply({ type: "serverData", data, isDirty });
    // Only new server data triggers a sync; typing does not.
  }, [data]);

  return {
    baseVersion: sync.current.base?.version ?? 0,
    changedOnServer: sync.current.pendingNewer !== null,
    markSaved: (saved: TData) => apply({ type: "saved", data: saved }),
    discard: () => {
      const newest = sync.current.pendingNewer ?? sync.current.base;
      if (newest) apply({ type: "saved", data: newest });
    },
    reload: async (refetch: () => Promise<{ data?: TData | undefined }>) => {
      const { data: newest } = await refetch();
      if (newest) apply({ type: "saved", data: newest });
    },
  };
}
