import { describe, expect, it } from "vitest";
import { dateInput, decimalInput, intInput, optionalText, requiredText, toInput } from "./schema";

const issues = (r: { success: boolean; error?: { issues: { message: string }[] } }) =>
  r.success ? [] : r.error!.issues.map((i) => i.message);

describe("form schema helpers", () => {
  it("requiredText trims and rejects empty", () => {
    const s = requiredText(5, "Unesi naziv.");
    expect(s.parse("  Lim ")).toBe("Lim");
    expect(issues(s.safeParse("   "))).toEqual(["Unesi naziv."]);
    expect(issues(s.safeParse("predugo"))).toEqual(["Najviše 5 znakova."]);
  });

  it("optionalText maps empty to null", () => {
    expect(optionalText(10).parse("  ")).toBeNull();
    expect(optionalText(10).parse(" S235 ")).toBe("S235");
  });

  it("decimalInput accepts comma and dot", () => {
    const s = decimalInput({ required: "Unesi količinu.", min: 0.001, minMessage: "Količina mora biti veća od 0." });
    expect(s.parse("12,5")).toBe(12.5);
    expect(s.parse("12.5")).toBe(12.5);
    expect(issues(s.safeParse(""))).toEqual(["Unesi količinu."]);
    expect(issues(s.safeParse("abc"))).toEqual(["Unesi broj, npr. 12,5."]);
    expect(issues(s.safeParse("0"))).toEqual(["Količina mora biti veća od 0."]);
    expect(decimalInput().parse("")).toBeNull();
  });

  it("intInput accepts whole numbers only", () => {
    const s = intInput({ min: 0 });
    expect(s.parse("7")).toBe(7);
    expect(s.parse("")).toBeNull();
    expect(issues(s.safeParse("2,5"))).toEqual(["Unesi cijeli broj."]);
    expect(issues(s.safeParse("-1"))).toEqual(["Najmanje 0."]);
  });

  it("dateInput", () => {
    expect(dateInput().parse("")).toBeNull();
    expect(dateInput().parse("2026-10-12")).toBe("2026-10-12");
    expect(issues(dateInput("Unesi rok.").safeParse(""))).toEqual(["Unesi rok."]);
  });

  it("toInput shows decimal comma", () => {
    expect(toInput(12.5)).toBe("12,5");
    expect(toInput(null)).toBe("");
  });
});
