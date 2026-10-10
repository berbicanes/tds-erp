/**
 * Edit forms vs. server refetches (e.g. TanStack Query refetch on window focus).
 *
 * Rule: a form takes data from the server only while the user has no unsaved changes. While the form is
 * dirty, newer server data is NOT applied (nothing the user typed is lost) and the form keeps the version
 * it was opened with, so saving answers with a 409 conflict instead of silently overwriting someone else.
 */

export interface Versioned {
  version: number;
}

export interface FormSyncState<T extends Versioned> {
  /** Server data the form values are based on (null = nothing loaded yet). */
  base: T | null;
  /** Newer server data that arrived while the form was dirty (shown as a notice). */
  pendingNewer: T | null;
}

export type FormSyncAction<T extends Versioned> =
  | { type: "serverData"; data: T; isDirty: boolean }
  | { type: "saved"; data: T };

export function initialFormSync<T extends Versioned>(): FormSyncState<T> {
  return { base: null, pendingNewer: null };
}

/** Returns the next state and whether the form values must be reset to the server data. */
export function formSync<T extends Versioned>(
  state: FormSyncState<T>,
  action: FormSyncAction<T>,
): { state: FormSyncState<T>; resetTo: T | null } {
  if (action.type === "saved") return { state: { base: action.data, pendingNewer: null }, resetTo: action.data };

  const { data, isDirty } = action;
  if (state.base === null) return { state: { base: data, pendingNewer: null }, resetTo: data };
  if (data.version === state.base.version) {
    // Same record version (refetch returned nothing new): never touch the form.
    return { state: { ...state, pendingNewer: null }, resetTo: null };
  }
  if (isDirty) return { state: { ...state, pendingNewer: data }, resetTo: null };
  return { state: { base: data, pendingNewer: null }, resetTo: data };
}
