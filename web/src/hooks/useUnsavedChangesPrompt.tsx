import { useEffect } from "react";
import { useBlocker } from "react-router";
import { ConfirmDialog } from "../components/ConfirmDialog";

/** UI §7: leaving a form with unsaved changes asks for confirmation. */
export function useUnsavedChangesPrompt(isDirty: boolean) {
  const blocker = useBlocker(({ currentLocation, nextLocation }) =>
    isDirty && currentLocation.pathname !== nextLocation.pathname);

  useEffect(() => {
    if (!isDirty) return;
    const onBeforeUnload = (e: BeforeUnloadEvent) => e.preventDefault();
    window.addEventListener("beforeunload", onBeforeUnload);
    return () => window.removeEventListener("beforeunload", onBeforeUnload);
  }, [isDirty]);

  return (
    <ConfirmDialog
      open={blocker.state === "blocked"}
      title="Napustiti formu?"
      confirmText="Napusti bez spremanja"
      danger
      onConfirm={() => blocker.proceed?.()}
      onCancel={() => blocker.reset?.()}
    >
      Izmjene nisu spremljene i biće izgubljene.
    </ConfirmDialog>
  );
}
