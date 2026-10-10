import { describe, expect, it } from "vitest";
import { todaySarajevo } from "./date";

describe("todaySarajevo", () => {
  it("uses the Sarajevo date, not UTC", () => {
    expect(todaySarajevo(new Date("2026-10-12T22:30:00Z"))).toBe("2026-10-13");
    expect(todaySarajevo(new Date("2026-10-12T21:59:00Z"))).toBe("2026-10-12");
  });
});
