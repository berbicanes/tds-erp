/** Thin fetch wrapper for the same-origin API. Errors are RFC 9457 ProblemDetails with Bosnian `detail`. */

export type FieldErrors = Record<string, string[]>;

export class ApiError extends Error {
  readonly status: number;
  readonly fieldErrors: FieldErrors;
  /** ProblemDetails extension "code", e.g. "concurrency" for a 409 edit conflict. */
  readonly code: string | null;

  constructor(status: number, detail: string, fieldErrors: FieldErrors = {}, code: string | null = null) {
    super(detail);
    this.name = "ApiError";
    this.status = status;
    this.fieldErrors = fieldErrors;
    this.code = code;
  }

  get isConcurrencyConflict(): boolean {
    return this.status === 409 && this.code === "concurrency";
  }
}

const NETWORK_ERROR = "Server nije dostupan. Provjeri internet vezu i pokušaj ponovo.";
const UNKNOWN_ERROR = "Došlo je do neočekivane greške. Pokušaj ponovo.";

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const body = (await response.json()) as { detail?: unknown; errors?: unknown; code?: unknown };
    const detail = typeof body.detail === "string" && body.detail ? body.detail : UNKNOWN_ERROR;
    const errors = body.errors && typeof body.errors === "object" ? (body.errors as FieldErrors) : {};
    const code = typeof body.code === "string" ? body.code : null;
    return new ApiError(response.status, detail, errors, code);
  } catch {
    return new ApiError(response.status, UNKNOWN_ERROR);
  }
}

export async function apiFetch<T>(path: string, init: RequestInit = {}): Promise<T> {
  const headers = new Headers(init.headers);
  headers.set("Accept", "application/json");
  if (init.body !== undefined && !(init.body instanceof FormData)) {
    headers.set("Content-Type", "application/json");
  }

  let response: Response;
  try {
    response = await fetch(path, { ...init, headers, credentials: "same-origin" });
  } catch {
    throw new ApiError(0, NETWORK_ERROR);
  }

  if (!response.ok) throw await toApiError(response);
  if (response.status === 204) return undefined as T;
  return (await response.json()) as T;
}

const withBody = (method: string, body?: unknown): RequestInit => ({
  method,
  ...(body === undefined ? {} : { body: body instanceof FormData ? body : JSON.stringify(body) }),
});

export const api = {
  get: <T>(path: string) => apiFetch<T>(path),
  post: <T>(path: string, body?: unknown) => apiFetch<T>(path, withBody("POST", body)),
  put: <T>(path: string, body?: unknown) => apiFetch<T>(path, withBody("PUT", body)),
};

/** Server list response (ListQuery / PagedResult on the backend). */
export interface Paged<T> {
  items: T[];
  total: number;
  page: number;
  pageSize: number;
}

/** Write responses with non-blocking warnings (e.g. "lot bez certifikata"). */
export interface WithWarnings<T> {
  data: T;
  warnings: string[];
}
