import { describe, expect, it } from "vitest";
import { formSync, initialFormSync, type FormSyncState } from "./formSync";

interface Emp { id: string; position: string; version: number }

const v1: Emp = { id: "e1", position: "Operater lasera", version: 1 };
const v2: Emp = { id: "e1", position: "Vođa smjene", version: 2 };

function run(steps: Parameters<typeof formSync<Emp>>[1][]) {
  let state: FormSyncState<Emp> = initialFormSync<Emp>();
  const resets: (Emp | null)[] = [];
  for (const s of steps) {
    const r = formSync(state, s);
    state = r.state;
    resets.push(r.resetTo);
  }
  return { state, resets };
}

describe("edit forms vs. server refetch", () => {
  it("first load fills the form", () => {
    const { state, resets } = run([{ type: "serverData", data: v1, isDirty: false }]);
    expect(resets).toEqual([v1]);
    expect(state.base).toBe(v1);
  });

  it("refetch with newer data while the user has unsaved changes does NOT reset the form and keeps the old version", () => {
    // Exactly the reproduced bug: user types, someone else saves, window focus → refetch brings version 2.
    const { state, resets } = run([
      { type: "serverData", data: v1, isDirty: false },
      { type: "serverData", data: v2, isDirty: true },
    ]);
    expect(resets).toEqual([v1, null]);
    expect(state.base?.version).toBe(1); // save will send version 1 → server answers 409, nothing is overwritten
    expect(state.pendingNewer).toBe(v2); // UI shows "changed in the meantime"
  });

  it("refetch with newer data while the form is clean takes the new data", () => {
    const { state, resets } = run([
      { type: "serverData", data: v1, isDirty: false },
      { type: "serverData", data: v2, isDirty: false },
    ]);
    expect(resets).toEqual([v1, v2]);
    expect(state.base?.version).toBe(2);
  });

  it("refetch of the same version never resets, dirty or not", () => {
    const { resets } = run([
      { type: "serverData", data: v1, isDirty: false },
      { type: "serverData", data: { ...v1 }, isDirty: true },
      { type: "serverData", data: { ...v1 }, isDirty: false },
    ]);
    expect(resets).toEqual([v1, null, null]);
  });

  it("after a successful save the saved data becomes the new base", () => {
    const saved: Emp = { ...v1, position: "Operater lasera i plazme", version: 3 };
    const { state, resets } = run([
      { type: "serverData", data: v1, isDirty: false },
      { type: "serverData", data: v2, isDirty: true },
      { type: "saved", data: saved },
    ]);
    expect(resets.at(-1)).toBe(saved);
    expect(state).toEqual({ base: saved, pendingNewer: null });
  });
});
