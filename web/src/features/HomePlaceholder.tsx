import { useLogout, useMe } from "../lib/auth";

/** Temporary until the layout (Phase 0, layout item) exists. */
export function HomePlaceholder() {
  const me = useMe();
  const logout = useLogout();
  return (
    <main className="space-y-3 p-4">
      <p>Prijavljen: {me.data?.userName}</p>
      <button type="button" className="h-12 rounded border border-slate-300 px-4" onClick={() => logout.mutate()}>
        Odjavi se
      </button>
    </main>
  );
}
