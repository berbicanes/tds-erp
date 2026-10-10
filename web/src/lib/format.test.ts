import { describe, expect, it } from "vitest";
import {
  EMPTY,
  formatDate,
  formatDateTime,
  formatHours,
  formatPercent,
  formatPieces,
  formatQuantity,
  formatTime,
  parseDecimal,
} from "./format";

describe("unknown is not zero (golden rule 6)", () => {
  it.each([formatQuantity, formatPieces, formatHours, formatPercent])("%o shows — for null/undefined", (fn) => {
    expect(fn(null)).toBe(EMPTY);
    expect(fn(undefined)).toBe(EMPTY);
  });

  it("dates show — for null, empty or invalid", () => {
    expect(formatDate(null)).toBe(EMPTY);
    expect(formatDate("")).toBe(EMPTY);
    expect(formatDate("12.10.2026")).toBe(EMPTY);
    expect(formatDateTime(undefined)).toBe(EMPTY);
    expect(formatDateTime("nije datum")).toBe(EMPTY);
    expect(formatTime(null)).toBe(EMPTY);
  });

  it("zero stays zero", () => {
    expect(formatQuantity(0)).toBe("0");
    expect(formatPieces(0)).toBe("0");
    expect(formatHours(0)).toBe("0");
  });
});

describe("formatQuantity", () => {
  it.each([
    [1234.5, "1.234,5"],
    [1234567.125, "1.234.567,125"],
    [3, "3"],
    [2.5, "2,5"],
    [0.001, "0,001"],
    [1.1, "1,1"],
    [0.1 + 0.2, "0,3"],
    [-12.75, "-12,75"],
    [999, "999"],
    [1000, "1.000"],
    [1.0004, "1"],
    [1.2346, "1,235"],
    [-0.0001, "0"],
  ])("%d → %s", (value, expected) => {
    expect(formatQuantity(value)).toBe(expected);
  });
});

describe("formatPieces", () => {
  it.each([
    [1234, "1.234"],
    [7, "7"],
    [1000000, "1.000.000"],
    [-3, "-3"],
  ])("%d → %s", (value, expected) => {
    expect(formatPieces(value)).toBe(expected);
  });
});

describe("formatHours", () => {
  it.each([
    [7.5, "7,5"],
    [8, "8"],
    [1.25, "1,25"],
    [1.255, "1,25"],
    [0.333, "0,33"],
  ])("%d → %s", (value, expected) => {
    expect(formatHours(value)).toBe(expected);
  });
});

describe("formatPercent", () => {
  it("rounds to a whole percent", () => {
    expect(formatPercent(42.4)).toBe("42 %");
    expect(formatPercent(100)).toBe("100 %");
  });
});

describe("formatDate", () => {
  it("formats DateOnly as dd.MM.yyyy. with trailing dot", () => {
    expect(formatDate("2026-10-12")).toBe("12.10.2026.");
    expect(formatDate("2026-01-05")).toBe("05.01.2026.");
  });

  it("does not shift the business date by time zone", () => {
    expect(formatDate("2026-12-31")).toBe("31.12.2026.");
  });
});

describe("formatDateTime / formatTime (Europe/Sarajevo)", () => {
  it("converts UTC to CEST in summer", () => {
    expect(formatDateTime("2026-10-12T12:32:00Z")).toBe("12.10.2026. 14:32");
    expect(formatTime("2026-10-12T12:32:00Z")).toBe("14:32");
  });

  it("converts UTC to CET in winter", () => {
    expect(formatDateTime("2026-12-01T12:32:00Z")).toBe("01.12.2026. 13:32");
  });

  it("rolls over to the next day after local midnight", () => {
    expect(formatDateTime("2026-12-31T23:30:00Z")).toBe("01.01.2027. 00:30");
  });

  it("accepts offsets", () => {
    expect(formatTime("2026-10-12T14:32:00+02:00")).toBe("14:32");
  });
});

describe("parseDecimal accepts comma and dot (UI §7)", () => {
  it.each([
    ["2,5", 2.5],
    ["2.5", 2.5],
    [" 12 ", 12],
    ["0,001", 0.001],
    ["-3,25", -3.25],
  ])("%s → %d", (input, expected) => {
    expect(parseDecimal(input)).toBe(expected);
  });

  it.each(["", "  ", "abc", "1,2,3", "1.234,5", "2,", ",5", "1e3"])("rejects %s", (input) => {
    expect(parseDecimal(input)).toBeNull();
  });
});
