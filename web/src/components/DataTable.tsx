import { isValidElement, useEffect, useState, type ReactNode } from "react";
import { Link } from "react-router";
import type { UseQueryResult } from "@tanstack/react-query";
import { api, type Paged } from "../lib/api";
import { downloadCsv, toCsv, type CsvValue } from "../lib/csv";
import { nextSort, sortDirection, toApiQuery, type ListState } from "../lib/listParams";
import { EMPTY, formatPieces } from "../lib/format";
import { useDebouncedValue } from "../hooks/useDebouncedValue";
import { ErrorBanner } from "./ErrorBanner";

export interface Column<T> {
  key: string;
  header: string;
  /** Server sort key; omit for unsortable columns. */
  sortKey?: string;
  align?: "left" | "right";
  render: (row: T) => ReactNode;
  /** CSV value (numbers via csvNumber, dates via csvDate); default: the rendered text. */
  csv?: (row: T) => CsvValue;
  /** Phone cards (UI §5): true = shown on the card; false = never. Default: the first 4 columns. */
  mobile?: boolean;
}

/** "—" (unknown) is an empty CSV cell, never 0 (golden rule 6). */
const blankIfUnknown = (t: string) => (t === EMPTY ? "" : t);

/** Visible text of a rendered cell, for CSV (badges carry their text in `label`). */
export function textOf(node: ReactNode): string {
  if (node == null || typeof node === "boolean") return "";
  if (typeof node === "string" || typeof node === "number") return String(node);
  if (Array.isArray(node)) return node.map(textOf).join("");
  if (isValidElement<{ children?: ReactNode; label?: { text?: string } }>(node)) {
    if (node.props.label?.text) return node.props.label.text;
    return textOf(node.props.children);
  }
  return "";
}

interface Props<T> {
  columns: Column<T>[];
  query: UseQueryResult<Paged<T>>;
  state: ListState;
  onStateChange: (next: ListState) => void;
  rowKey: (row: T) => string;
  rowHref?: (row: T) => string;
  /** Empty state text, e.g. "Nema naloga." (UI §1: never an empty space). */
  emptyText: string;
  searchPlaceholder?: string;
  /** Filter chips rendered above the table. */
  toolbar?: ReactNode;
  /** CSV export (UI §5): API list path with the same filters, and the file name without extension. */
  exportPath?: string;
  exportName?: string;
}

/** Shared list table (UI §5): server paging (50), sort per column, debounced search, state in URL. */
export function DataTable<T>({ columns, query, state, onStateChange, rowKey, rowHref, emptyText, searchPlaceholder, toolbar, exportPath, exportName }: Props<T>) {
  const [search, setSearch] = useState(state.q);
  const [exporting, setExporting] = useState(false);
  const [exportError, setExportError] = useState<unknown>(null);

  const marked = columns.filter((c, i) => i > 0 && c.mobile === true);
  const cardColumns = marked.length > 0 ? marked : columns.slice(1).filter((c) => c.mobile !== false).slice(0, 3);

  const exportCsv = async () => {
    if (!exportPath) return;
    setExporting(true);
    setExportError(null);
    try {
      const rows: T[] = [];
      for (let page = 1; ; page++) {
        const result = await api.get<Paged<T>>(`${exportPath}?${toApiQuery({ ...state, page }, 200)}`);
        rows.push(...result.items);
        if (rows.length >= result.total || result.items.length === 0) break;
      }
      const csv = toCsv(columns.map((c) => c.header), rows.map((r) => columns.map((c) => (c.csv ? c.csv(r) : blankIfUnknown(textOf(c.render(r)))))));
      downloadCsv(`${exportName ?? "lista"}.csv`, csv);
    } catch (e) {
      setExportError(e);
    } finally {
      setExporting(false);
    }
  };
  const debounced = useDebouncedValue(search, 300);

  // Keep the input in sync when the URL changes (back button).
  useEffect(() => setSearch(state.q), [state.q]);
  useEffect(() => {
    if (debounced !== state.q) onStateChange({ ...state, q: debounced, page: 1 });
  }, [debounced]);

  const data = query.data;
  const pages = data ? Math.max(1, Math.ceil(data.total / data.pageSize)) : 1;
  const from = data && data.total > 0 ? (data.page - 1) * data.pageSize + 1 : 0;
  const to = data ? Math.min(data.total, data.page * data.pageSize) : 0;

  return (
    <div className="space-y-3 px-4 py-3">
      <div className="flex flex-wrap items-center gap-2">
        <input
          type="search"
          value={search}
          onChange={(e) => setSearch(e.target.value)}
          placeholder={searchPlaceholder ?? "Pretraga"}
          aria-label="Pretraga"
          className="h-10 w-full max-w-xs rounded border border-slate-300 px-3 text-sm"
        />
        {toolbar}
        {exportPath && (
          <button type="button" onClick={() => void exportCsv()} disabled={exporting || !query.data?.total}
            className="ml-auto min-h-10 rounded border border-slate-300 px-3 text-sm disabled:opacity-50">
            {exporting ? "Izvoz…" : "Izvezi CSV"}
          </button>
        )}
      </div>

      <ErrorBanner error={query.error ?? exportError} onRefresh={() => void query.refetch()} />

      {/* UI §5: on phones rows become cards with the most important fields. */}
      <ul className="space-y-2 sm:hidden" aria-label="Lista">
        {query.isPending && <li className="py-6 text-center text-slate-500">Učitavanje…</li>}
        {data && data.items.length === 0 && (
          <li className="py-6 text-center text-slate-600">{state.q || Object.keys(state.filters).length ? "Nema rezultata za ovu pretragu ili filter." : emptyText}</li>
        )}
        {data?.items.map((row) => {
          const title = columns[0]!.render(row);
          return (
            <li key={rowKey(row)} className="rounded border border-slate-200 p-3 text-sm">
              {rowHref ? (
                <Link to={rowHref(row)} className="block min-h-6 font-semibold text-blue-800">{title}</Link>
              ) : <div className="font-semibold">{title}</div>}
              <dl className="mt-1 grid grid-cols-[auto_1fr] gap-x-3 gap-y-0.5">
                {cardColumns.map((c) => (
                  <div key={c.key} className="contents">
                    <dt className="text-slate-500">{c.header}</dt>
                    <dd className="min-w-0 break-words">{c.render(row)}</dd>
                  </div>
                ))}
              </dl>
            </li>
          );
        })}
      </ul>

      <div className="hidden overflow-x-auto rounded border border-slate-200 sm:block">
        <table className="w-full border-collapse text-sm">
          <thead className="bg-slate-50 text-left text-xs text-slate-600">
            <tr>
              {columns.map((c) => {
                const dir = c.sortKey ? sortDirection(state.sort, c.sortKey) : null;
                return (
                  <th
                    key={c.key}
                    scope="col"
                    aria-sort={dir === "asc" ? "ascending" : dir === "desc" ? "descending" : undefined}
                    className={`whitespace-nowrap border-b border-slate-200 px-2 py-2 font-medium ${c.align === "right" ? "text-right" : ""}`}
                  >
                    {c.sortKey ? (
                      <button
                        type="button"
                        className="inline-flex items-center gap-1 hover:text-slate-900"
                        onClick={() => onStateChange({ ...state, sort: nextSort(state.sort, c.sortKey!), page: 1 })}
                      >
                        {c.header}
                        <span aria-hidden className="w-3 text-slate-400">{dir === "asc" ? "▲" : dir === "desc" ? "▼" : ""}</span>
                      </button>
                    ) : (
                      c.header
                    )}
                  </th>
                );
              })}
            </tr>
          </thead>
          <tbody>
            {query.isPending && (
              <tr>
                <td colSpan={columns.length} className="px-2 py-6 text-center text-slate-500">Učitavanje…</td>
              </tr>
            )}
            {data && data.items.length === 0 && (
              <tr>
                <td colSpan={columns.length} className="px-2 py-6 text-center text-slate-600">
                  {state.q || Object.keys(state.filters).length ? "Nema rezultata za ovu pretragu ili filter." : emptyText}
                </td>
              </tr>
            )}
            {data?.items.map((row) => (
              <tr key={rowKey(row)} className="border-b border-slate-100 hover:bg-slate-50">
                {columns.map((c, i) => (
                  <td key={c.key} className={`px-2 py-1.5 ${c.align === "right" ? "text-right tabular-nums" : ""}`}>
                    {i === 0 && rowHref ? (
                      <Link to={rowHref(row)} className="font-medium text-blue-800 hover:underline">
                        {c.render(row)}
                      </Link>
                    ) : (
                      c.render(row)
                    )}
                  </td>
                ))}
              </tr>
            ))}
          </tbody>
        </table>
      </div>

      {data && data.total > 0 && (
        <div className="flex items-center justify-between gap-2 text-sm text-slate-600">
          <span className="tabular-nums">
            {formatPieces(from)}–{formatPieces(to)} od {formatPieces(data.total)}
          </span>
          <div className="flex gap-2">
            <button
              type="button"
              disabled={state.page <= 1}
              onClick={() => onStateChange({ ...state, page: state.page - 1 })}
              className="min-h-10 rounded border border-slate-300 px-3 disabled:opacity-40"
            >
              Prethodna
            </button>
            <button
              type="button"
              disabled={state.page >= pages}
              onClick={() => onStateChange({ ...state, page: state.page + 1 })}
              className="min-h-10 rounded border border-slate-300 px-3 disabled:opacity-40"
            >
              Sljedeća
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
