/**
 * UI §5 CSV export, so Excel in BiH opens it correctly: UTF-8 with BOM, ";" separator, decimal comma, CRLF.
 * Unknown values are empty cells (never 0).
 */
export type CsvValue = string | number | boolean | null | undefined | CsvNumber | CsvDate;

/** A number to be written with a decimal comma and no thousands separator (Excel reads it as a number). */
export interface CsvNumber { kind: "number"; value: number | null | undefined }
/** A business date ("2026-10-12") written as 12.10.2026 (Excel reads it as a date). */
export interface CsvDate { kind: "date"; value: string | null | undefined }

export const csvNumber = (value: number | null | undefined): CsvNumber => ({ kind: "number", value });
export const csvDate = (value: string | null | undefined): CsvDate => ({ kind: "date", value });

function text(v: CsvValue): string {
  if (v == null) return "";
  if (typeof v === "boolean") return v ? "Da" : "Ne";
  if (typeof v === "number") return Number.isFinite(v) ? String(v).replace(".", ",") : "";
  if (typeof v === "object") {
    if (v.kind === "number") return v.value == null || !Number.isFinite(v.value) ? "" : String(v.value).replace(".", ",");
    const m = v.value ? /^(\d{4})-(\d{2})-(\d{2})/.exec(v.value) : null;
    return m ? `${m[3]}.${m[2]}.${m[1]}` : "";
  }
  // Text that Excel would run as a formula is prefixed (CSV injection).
  return /^[=+\-@\t\r]/.test(v) ? `'${v}` : v;
}

function cell(v: CsvValue): string {
  const t = text(v);
  return /[;"\r\n]/.test(t) || t !== t.trim() ? `"${t.replace(/"/g, '""')}"` : t;
}

export function toCsv(headers: string[], rows: CsvValue[][]): string {
  return "﻿" + [headers, ...rows].map((r) => r.map(cell).join(";")).join("\r\n") + "\r\n";
}

/** Starts a download of the CSV in the browser. */
export function downloadCsv(fileName: string, content: string): void {
  const url = URL.createObjectURL(new Blob([content], { type: "text/csv;charset=utf-8" }));
  const a = document.createElement("a");
  a.href = url;
  a.download = fileName;
  document.body.appendChild(a);
  a.click();
  a.remove();
  setTimeout(() => URL.revokeObjectURL(url), 1000);
}
