import { describe, expect, it } from "vitest";
import { formatHistoryValue, historyLines } from "./history";
import type { HistoryEntry } from "./historyApi";
import { inspectionPhase } from "./labels";

const entry = (entityType: string, changes: HistoryEntry["changes"], action: HistoryEntry["action"] = "Update"): HistoryEntry => ({
  id: "1", at: "2026-10-12T12:32:00Z", userName: "anes", action, entityType, entityId: "x", recordLabel: "Nalog RN-2026-0001", reason: null, changes,
});

describe("history (UI §6, no raw JSON)", () => {
  it("formats enums by record, dates, quantities and lists", () => {
    expect(historyLines(entry("WorkOrder", [{ field: "Status", old: "Laser", new: "Presanje" }]))).toEqual([{ label: "Status", old: "Laser", new: "Presanje" }]);
    expect(historyLines(entry("NonConformity", [{ field: "Status", old: "RadnjaUToku", new: "ProvjeraEfektivnosti" }]))[0])
      .toEqual({ label: "Status", old: "Radnja u toku", new: "Provjera efektivnosti" });
    expect(historyLines(entry("ControlledDocument", [{ field: "Status", old: "Odobren", new: "Povucen" }]))[0]!.new).toBe("Povučen");
    expect(historyLines(entry("ProductionEntry", [{ field: "EquipmentIds", old: ["M-01 Laser"], new: ["M-01 Laser", "M-03 Presa 350 t"] }]))[0])
      .toEqual({ label: "Oprema", old: "M-01 Laser", new: "M-01 Laser, M-03 Presa 350 t" });
    expect(formatHistoryValue("date", "2026-10-12")).toBe("12.10.2026.");
    expect(formatHistoryValue("qty", 1234.5)).toBe("1.234,5");
    expect(formatHistoryValue(inspectionPhase, "HidraulicnoPresanje")).toBe("Hidraulično presanje");
    expect(formatHistoryValue("text", null)).toBe("—");
    expect(formatHistoryValue("text", true)).toBe("Da");
  });

  it("shows unknown fields by name instead of hiding them", () => {
    expect(historyLines(entry("WorkOrder", [{ field: "NovoPolje", old: null, new: "x" }]))[0]).toEqual({ label: "NovoPolje", old: "—", new: "x" });
  });
});
