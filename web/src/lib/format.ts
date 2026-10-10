/**
 * Display formatting (CLAUDE.md "Formatiranje u UI"):
 * date dd.MM.yyyy., decimal comma, "." thousands separator, "—" when the value is unknown (golden rule 6).
 */

export const EMPTY = "—";

const BUSINESS_TIME_ZONE = "Europe/Sarajevo";

type Maybe<T> = T | null | undefined;

function groupThousands(integerDigits: string): string {
  return integerDigits.replace(/\B(?=(\d{3})+(?!\d))/g, ".");
}

/** Fixed-point formatting without float artefacts; trailing zeros removed. */
function formatDecimal(value: number, maxFractionDigits: number): string {
  if (!Number.isFinite(value)) return EMPTY;
  const fixed = Math.abs(value).toFixed(maxFractionDigits);
  const [intPart = "0", fracPart = ""] = fixed.split(".");
  const fraction = fracPart.replace(/0+$/, "");
  const isZero = /^0*$/.test(intPart) && fraction === "";
  const sign = value < 0 && !isZero ? "-" : "";
  return sign + groupThousands(intPart) + (fraction ? "," + fraction : "");
}

/** Material quantity: up to 3 decimals, no trailing zeros. 1234.5 → "1.234,5". */
export function formatQuantity(value: Maybe<number>): string {
  return value == null ? EMPTY : formatDecimal(value, 3);
}

/** Pieces are whole numbers. 1234 → "1.234". */
export function formatPieces(value: Maybe<number>): string {
  return value == null ? EMPTY : formatDecimal(Math.round(value), 0);
}

/** Labour hours: up to 2 decimals. 7.5 → "7,5". */
export function formatHours(value: Maybe<number>): string {
  return value == null ? EMPTY : formatDecimal(value, 2);
}

/** Integer percent, e.g. progress. 42 → "42 %". */
export function formatPercent(value: Maybe<number>): string {
  return value == null ? EMPTY : `${formatDecimal(Math.round(value), 0)} %`;
}

/** Business date from the API ("2026-10-12", DateOnly) → "12.10.2026.". No time zone shift. */
export function formatDate(value: Maybe<string>): string {
  if (!value) return EMPTY;
  const match = /^(\d{4})-(\d{2})-(\d{2})/.exec(value);
  if (!match) return EMPTY;
  const [, y, m, d] = match;
  return `${d}.${m}.${y}.`;
}

function sarajevoParts(value: string): Record<string, string> | null {
  const date = new Date(value);
  if (Number.isNaN(date.getTime())) return null;
  const parts = new Intl.DateTimeFormat("en-GB", {
    timeZone: BUSINESS_TIME_ZONE,
    year: "numeric",
    month: "2-digit",
    day: "2-digit",
    hour: "2-digit",
    minute: "2-digit",
    hourCycle: "h23",
  }).formatToParts(date);
  return Object.fromEntries(parts.map((p) => [p.type, p.value]));
}

/** UTC timestamp → "12.10.2026. 14:32" in Europe/Sarajevo. */
export function formatDateTime(value: Maybe<string>): string {
  const p = value ? sarajevoParts(value) : null;
  return p ? `${p.day}.${p.month}.${p.year}. ${p.hour}:${p.minute}` : EMPTY;
}

/** UTC timestamp → "14:32" in Europe/Sarajevo. */
export function formatTime(value: Maybe<string>): string {
  const p = value ? sarajevoParts(value) : null;
  return p ? `${p.hour}:${p.minute}` : EMPTY;
}

/**
 * Number input: accepts both comma and dot as decimal separator (UI §7).
 * Returns null for empty or invalid input. Thousands separators are not accepted ("1.234,5" is invalid).
 */
export function parseDecimal(input: string): number | null {
  const s = input.trim().replace(/\s/g, "");
  if (!/^-?\d+([.,]\d+)?$/.test(s)) return null;
  return Number(s.replace(",", "."));
}
