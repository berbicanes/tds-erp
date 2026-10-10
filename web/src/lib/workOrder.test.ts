import { describe, expect, it } from "vitest";
import { dueTone, isFinished } from "./workOrder";

describe("dueTone (UI §3)", () => {
  const today = "2026-10-12";
  it("red when overdue, amber within 3 days, none otherwise", () => {
    expect(dueTone("2026-10-10", true, false, today)).toBe("red");
    expect(dueTone("2026-10-12", false, false, today)).toBe("amber");
    expect(dueTone("2026-10-15", false, false, today)).toBe("amber");
    expect(dueTone("2026-10-16", false, false, today)).toBeNull();
  });
  it("finished orders have no colour", () => {
    expect(dueTone("2026-10-01", false, true, today)).toBeNull();
  });
  it("isFinished", () => {
    expect(isFinished("Isporuceno")).toBe(true);
    expect(isFinished("Laser")).toBe(false);
  });
});
