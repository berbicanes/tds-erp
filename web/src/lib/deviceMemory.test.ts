// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { isString, isStringArray, pushRecent, readMemory, writeMemory } from "./deviceMemory";

afterEach(() => {
  vi.restoreAllMocks();
  window.localStorage.clear();
});

describe("deviceMemory (UI §4, localStorage in try/catch)", () => {
  it("round-trips values and validates their shape", () => {
    writeMemory("quick.operator", "e1");
    expect(readMemory("quick.operator", isString)).toBe("e1");
    writeMemory("quick.equipment", ["m1", "m2"]);
    expect(readMemory("quick.equipment", isStringArray)).toEqual(["m1", "m2"]);
    window.localStorage.setItem("tds.quick.operator", "{not json");
    expect(readMemory("quick.operator", isString)).toBeNull();
    writeMemory("quick.operator", 42);
    expect(readMemory("quick.operator", isString)).toBeNull();
  });

  it("never throws when storage is blocked", () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => { throw new Error("SecurityError"); });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => { throw new Error("QuotaExceededError"); });
    expect(() => writeMemory("quick.operator", "e1")).not.toThrow();
    expect(readMemory("quick.operator", isString)).toBeNull();
  });

  it("keeps the last three orders, most recent first", () => {
    expect(pushRecent(["a", "b", "c"], "d")).toEqual(["d", "a", "b"]);
    expect(pushRecent(["a", "b", "c"], "b")).toEqual(["b", "a", "c"]);
  });
});
