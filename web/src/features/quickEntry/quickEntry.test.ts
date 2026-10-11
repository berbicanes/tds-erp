import { describe, expect, it } from "vitest";
import { quickEntryProblems, type QuickEntryInput } from "./quickEntry";

const ok: QuickEntryInput = { orderId: "o", operatorId: "e", equipmentIds: ["m"], lotId: null, laser: "", pressed: "20", good: "", scrap: "", hours: "2,5", consumed: "" };

describe("quickEntryProblems (DOMAIN §5, UI §4)", () => {
  it("accepts a normal press entry", () => expect(quickEntryProblems(ok)).toEqual([]));
  it("needs a lot when cutting or consuming", () => {
    expect(quickEntryProblems({ ...ok, laser: "10" })).toContain("Odaberi lot materijala — obavezan je kad se reže ili troši materijal.");
    expect(quickEntryProblems({ ...ok, consumed: "1,5" })).toContain("Odaberi lot materijala — obavezan je kad se reže ili troši materijal.");
    expect(quickEntryProblems({ ...ok, laser: "10", lotId: "l" })).toEqual([]);
  });
  it("says what is missing", () => {
    expect(quickEntryProblems({ ...ok, orderId: null, operatorId: null, equipmentIds: [], pressed: "", hours: "" })).toEqual([
      "Odaberi nalog.", "Odaberi operatera.", "Odaberi barem jednu mašinu.",
      "Unesi barem jednu količinu: laser, presa, gotovo ili škart.", "Unesi radne sate.",
    ]);
    expect(quickEntryProblems({ ...ok, hours: "25" })).toEqual(["Radni sati moraju biti između 0 i 24."]);
    expect(quickEntryProblems({ ...ok, hours: "0" })).toEqual([]);
  });
});
