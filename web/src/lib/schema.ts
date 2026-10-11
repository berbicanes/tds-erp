import { z } from "zod";
import { parseDecimal } from "./format";

/**
 * Zod building blocks for forms. Inputs are strings (RHF); output is what the API expects.
 * Messages are Bosnian and say what to fix (UI §9).
 */

export const requiredText = (max: number, emptyMessage: string) =>
  z.string().trim().min(1, emptyMessage).max(max, `Najviše ${max} znakova.`);

/** Empty → null. */
export const optionalText = (max: number) =>
  z.string().trim().max(max, `Najviše ${max} znakova.`).transform((v) => (v === "" ? null : v));

/** Accepts "2,5" and "2.5" (UI §7). */
export const decimalInput = (opts: { required?: string; min?: number; minMessage?: string; max?: number; maxMessage?: string } = {}) =>
  z.string().transform((raw, ctx) => {
    if (raw.trim() === "") {
      if (opts.required) ctx.addIssue({ code: "custom", message: opts.required });
      return null;
    }
    const n = parseDecimal(raw);
    if (n === null) {
      ctx.addIssue({ code: "custom", message: "Unesi broj, npr. 12,5." });
      return null;
    }
    if (opts.min !== undefined && n < opts.min) ctx.addIssue({ code: "custom", message: opts.minMessage ?? `Najmanje ${opts.min}.` });
    if (opts.max !== undefined && n > opts.max) ctx.addIssue({ code: "custom", message: opts.maxMessage ?? `Najviše ${opts.max}.` });
    return n;
  });

/** Whole numbers (pieces). */
export const intInput = (opts: { required?: string; min?: number; minMessage?: string } = {}) =>
  z.string().transform((raw, ctx) => {
    const s = raw.trim();
    if (s === "") {
      if (opts.required) ctx.addIssue({ code: "custom", message: opts.required });
      return null;
    }
    if (!/^-?\d+$/.test(s)) {
      ctx.addIssue({ code: "custom", message: "Unesi cijeli broj." });
      return null;
    }
    const n = Number(s);
    if (opts.min !== undefined && n < opts.min) ctx.addIssue({ code: "custom", message: opts.minMessage ?? `Najmanje ${opts.min}.` });
    return n;
  });

/** <input type="date"> value "YYYY-MM-DD"; empty → null. */
export const dateInput = (required?: string) =>
  z.string().transform((raw, ctx) => {
    if (raw === "") {
      if (required) ctx.addIssue({ code: "custom", message: required });
      return null;
    }
    if (!/^\d{4}-\d{2}-\d{2}$/.test(raw)) {
      ctx.addIssue({ code: "custom", message: "Datum nije ispravan. Odaberi datum u kalendaru." });
      return null;
    }
    return raw;
  });

/** Number → form string with decimal comma (for editing existing values). */
export function toInput(value: number | null | undefined): string {
  return value == null ? "" : String(value).replace(".", ",");
}
