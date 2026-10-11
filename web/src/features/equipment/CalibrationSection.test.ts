import { describe, expect, it } from "vitest";
import { proposeNext } from "./CalibrationSection";

describe("proposeNext (same as server EquipmentCalibration.Record)", () => {
  it("adds the interval in months and clamps month ends", () => {
    expect(proposeNext("2026-03-15", 12)).toBe("2027-03-15");
    expect(proposeNext("2026-01-31", 1)).toBe("2026-02-28");
    expect(proposeNext("2026-03-15", null)).toBe("");
  });
});
