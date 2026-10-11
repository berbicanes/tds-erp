/**
 * UI §4: things remembered on this device (operator, machines, last orders). localStorage can be missing,
 * full or blocked (private mode), so every access is wrapped; the screen works without it.
 */
const PREFIX = "tds.";

function storage(): Storage | null {
  try {
    return typeof window === "undefined" ? null : window.localStorage;
  } catch {
    return null;
  }
}

export function readMemory<T>(key: string, isValid: (v: unknown) => v is T): T | null {
  try {
    const raw = storage()?.getItem(PREFIX + key);
    if (raw == null) return null;
    const value: unknown = JSON.parse(raw);
    return isValid(value) ? value : null;
  } catch {
    return null;
  }
}

export function writeMemory(key: string, value: unknown): void {
  try {
    storage()?.setItem(PREFIX + key, JSON.stringify(value));
  } catch {
    // Not remembered; nothing else depends on it.
  }
}

export const isString = (v: unknown): v is string => typeof v === "string" && v.length > 0;
export const isStringArray = (v: unknown): v is string[] => Array.isArray(v) && v.every((x) => typeof x === "string");

/** Most recent first, without duplicates, at most `max`. */
export function pushRecent(list: string[], id: string, max = 3): string[] {
  return [id, ...list.filter((x) => x !== id)].slice(0, max);
}
