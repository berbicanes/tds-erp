/** Thin fetch wrapper for the same-origin API. Errors are RFC 9457 ProblemDetails with Bosnian `detail`. */

export type FieldErrors = Record<string, string[]>;

export class ApiError extends Error {
  readonly status: number;
  readonly fieldErrors: FieldErrors;

  constructor(status: number, detail: string, fieldErrors: FieldErrors = {}) {
    super(detail);
    this.name = "ApiError";
    this.status = status;
    this.fieldErrors = fieldErrors;
  }
}

const NETWORK_ERROR = "Server nije dostupan. Provjeri internet vezu i pokušaj ponovo.";
const UNKNOWN_ERROR = "Došlo je do neočekivane greške. Pokušaj ponovo.";

async function toApiError(response: Response): Promise<ApiError> {
  try {
    const body = (await response.json()) as { detail?: unknown; errors?: unknown };
    const detail = typeof body.detail === "string" && body.detail ? body.detail : UNKNOWN_ERROR;
    const errors = body.errors && typeof body.errors === "object" ? (body.errors as FieldErrors) : {};
    return new ApiError(response.status, detail, errors);
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

export const api = {
  get: <T>(path: string) => apiFetch<T>(path),
  post: <T>(path: string, body?: unknown) =>
    apiFetch<T>(path, { method: "POST", ...(body === undefined ? {} : { body: JSON.stringify(body) }) }),
};
