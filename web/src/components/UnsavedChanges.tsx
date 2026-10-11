import { createContext, useCallback, useContext, useEffect, useId, useMemo, useRef, useState, type ReactNode } from "react";
import { useBlocker } from "react-router";
import { ConfirmDialog } from "./ConfirmDialog";

/**
 * UI §7: leaving a page with unsaved changes asks for confirmation.
 * One blocker for the whole app (React Router allows only one active blocker); every form on the page
 * registers its dirty state, so pages with several small forms (lot detail) are covered too.
 */
const DirtyContext = createContext<((id: string, dirty: boolean) => void) | null>(null);

export function UnsavedChangesProvider({ children }: { children: ReactNode }) {
  // The ref is the truth for the blocker (updated synchronously, e.g. right before navigating after a save);
  // the state only re-renders for the beforeunload listener.
  const dirtyRef = useRef(new Set<string>());
  const [anyDirty, setAnyDirty] = useState(false);

  const register = useCallback((id: string, isDirty: boolean) => {
    if (isDirty) dirtyRef.current.add(id);
    else dirtyRef.current.delete(id);
    setAnyDirty(dirtyRef.current.size > 0);
  }, []);

  const blocker = useBlocker(({ currentLocation, nextLocation }) =>
    dirtyRef.current.size > 0 && currentLocation.pathname !== nextLocation.pathname);

  useEffect(() => {
    if (!anyDirty) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener("beforeunload", onBeforeUnload);
    return () => window.removeEventListener("beforeunload", onBeforeUnload);
  }, [anyDirty]);

  return (
    <DirtyContext.Provider value={register}>
      {children}
      <ConfirmDialog
        open={blocker.state === "blocked"}
        title="Napustiti stranicu?"
        confirmText="Napusti bez spremanja"
        danger
        onConfirm={() => {
          dirtyRef.current.clear();
          setAnyDirty(false);
          blocker.proceed?.();
        }}
        onCancel={() => blocker.reset?.()}
      >
        Izmjene nisu spremljene i bit će izgubljene.
      </ConfirmDialog>
    </DirtyContext.Provider>
  );
}

/**
 * Registers a form's unsaved state with the page-wide confirmation.
 * Returns `markSaved()`: call it in a save's onSuccess before navigating away, so the confirmation does not
 * appear for changes that were just saved.
 */
export function useUnsavedChanges(isDirty: boolean) {
  const id = useId();
  const register = useContext(DirtyContext);
  useEffect(() => {
    register?.(id, isDirty);
  }, [register, id, isDirty]);
  useEffect(() => () => register?.(id, false), [register, id]);
  return useCallback(() => register?.(id, false), [register, id]);
}

/** Shown when the record changed on the server while the user had unsaved changes (lib/formSync.ts). */
export function ChangedOnServerNotice({ show, onDiscard }: { show: boolean; onDiscard: () => void }) {
  const text = useMemo(
    () => "Ovaj zapis je u međuvremenu izmijenio neko drugi. Tvoje izmjene nisu izgubljene, ali spremanje će javiti sukob.",
    [],
  );
  if (!show) return null;
  return (
    <div role="status" className="flex flex-wrap items-center gap-3 rounded bg-amber-50 p-3 text-sm text-amber-900">
      <span className="flex-1">{text}</span>
      <button type="button" onClick={onDiscard} className="min-h-10 rounded border border-amber-300 bg-white px-3 font-medium">
        Odbaci moje izmjene i učitaj novo
      </button>
    </div>
  );
}
