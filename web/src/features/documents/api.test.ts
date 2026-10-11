import { describe, expect, it } from "vitest";
import { nextVersionLabel } from "./api";

describe("nextVersionLabel", () => {
  it("increments the last number and keeps padding", () => {
    expect(nextVersionLabel("01")).toBe("02");
    expect(nextVersionLabel("09")).toBe("10");
    expect(nextVersionLabel("1.3")).toBe("1.4");
    expect(nextVersionLabel("A")).toBe("");
  });
});
