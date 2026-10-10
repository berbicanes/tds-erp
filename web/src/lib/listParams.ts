/**
 * List state lives in the URL (UI §5: shareable links, back button works).
 * ?page=2&sort=-dueDate&q=lim&status=Laser&status=Presanje
 */

export interface ListState {
  page: number;
  sort: string | null;
  q: string;
  /** Filter chips: key → selected values (multi-select). */
  filters: Record<string, string[]>;
}

const RESERVED = new Set(["page", "sort", "q"]);

export function readListState(params: URLSearchParams): ListState {
  const page = Number(params.get("page"));
  const filters: Record<string, string[]> = {};
  for (const [key, value] of params) {
    if (RESERVED.has(key) || value === "") continue;
    (filters[key] ??= []).push(value);
  }
  return {
    page: Number.isInteger(page) && page > 1 ? page : 1,
    sort: params.get("sort") || null,
    q: params.get("q") ?? "",
    filters,
  };
}

export function writeListState(state: ListState): URLSearchParams {
  const params = new URLSearchParams();
  if (state.page > 1) params.set("page", String(state.page));
  if (state.sort) params.set("sort", state.sort);
  if (state.q.trim()) params.set("q", state.q.trim());
  for (const key of Object.keys(state.filters).sort()) {
    for (const value of state.filters[key] ?? []) params.append(key, value);
  }
  return params;
}

/** Click on a column header: none → ascending → descending → ascending … */
export function nextSort(current: string | null, key: string): string {
  return current === key ? `-${key}` : key;
}

export function sortDirection(current: string | null, key: string): "asc" | "desc" | null {
  if (current === key) return "asc";
  if (current === `-${key}`) return "desc";
  return null;
}

export function toggleFilter(state: ListState, key: string, value: string): ListState {
  const current = state.filters[key] ?? [];
  const next = current.includes(value) ? current.filter((v) => v !== value) : [...current, value];
  const filters = { ...state.filters, [key]: next };
  if (next.length === 0) delete filters[key];
  return { ...state, filters, page: 1 };
}

/** Query string for the API: same keys plus pageSize. */
export function toApiQuery(state: ListState, pageSize = 50): string {
  const params = writeListState(state);
  params.set("page", String(state.page));
  params.set("pageSize", String(pageSize));
  return params.toString();
}
