import { describe, expect, it } from "vitest";
import { csvDate, csvNumber, toCsv } from "./csv";

describe("toCsv (UI §5: Excel in BiH)", () => {
  it("writes BOM, semicolons, CRLF, decimal comma and dates Excel understands", () => {
    const csv = toCsv(["Lot", "Količina", "Datum", "Napomena"], [
      ["H-4471", csvNumber(1234.5), csvDate("2026-10-12"), "Lim; crni"],
      ["L-2", csvNumber(null), csvDate(null), 'Kaže "OK"'],
    ]);
    expect(csv.startsWith("﻿")).toBe(true);
    expect(csv.slice(1)).toBe(
      "Lot;Količina;Datum;Napomena\r\n" +
      'H-4471;1234,5;12.10.2026;"Lim; crni"\r\n' +
      'L-2;;;"Kaže ""OK"""\r\n',
    );
  });

  it("keeps unknown values empty (never 0), quotes line breaks and neutralises formulas", () => {
    const csv = toCsv(["A", "B", "C", "D"], [[null, "red 1\nred 2", "=SUM(A1)", 0]]).slice(1);
    expect(csv).toBe('A;B;C;D\r\n;"red 1\nred 2";\'=SUM(A1);0\r\n');
    expect(toCsv(["X"], [[-12.5], [true]]).slice(1)).toBe("X\r\n-12,5\r\nDa\r\n");
  });
});
