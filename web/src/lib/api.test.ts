import { afterEach, describe, expect, it, vi } from "vitest";
import { ApiError, api } from "./api";

function mockFetch(response: Response | Error) {
  vi.stubGlobal(
    "fetch",
    vi.fn(() => (response instanceof Error ? Promise.reject(response) : Promise.resolve(response))),
  );
}

afterEach(() => vi.unstubAllGlobals());

describe("apiFetch", () => {
  it("returns parsed JSON on success", async () => {
    mockFetch(new Response(JSON.stringify({ userName: "admin" }), { status: 200 }));
    await expect(api.get("/api/me")).resolves.toEqual({ userName: "admin" });
  });

  it("returns undefined for 204", async () => {
    mockFetch(new Response(null, { status: 204 }));
    await expect(api.post("/api/auth/logout")).resolves.toBeUndefined();
  });

  it("turns ProblemDetails into ApiError with Bosnian detail and field errors", async () => {
    mockFetch(
      new Response(
        JSON.stringify({ status: 400, detail: "Neka polja nisu ispravno popunjena.", errors: { userName: ["Unesi korisničko ime."] } }),
        { status: 400, headers: { "Content-Type": "application/problem+json" } },
      ),
    );
    const error = await api.post("/api/auth/login", {}).catch((e: unknown) => e);
    expect(error).toBeInstanceOf(ApiError);
    expect((error as ApiError).status).toBe(400);
    expect((error as ApiError).message).toBe("Neka polja nisu ispravno popunjena.");
    expect((error as ApiError).fieldErrors).toEqual({ userName: ["Unesi korisničko ime."] });
  });

  it("uses a Bosnian fallback when the body is not JSON", async () => {
    mockFetch(new Response("<html>502</html>", { status: 502 }));
    const error = (await api.get("/api/me").catch((e: unknown) => e)) as ApiError;
    expect(error.status).toBe(502);
    expect(error.message).toBe("Došlo je do neočekivane greške. Pokušaj ponovo.");
  });

  it("reports network failure as status 0 with a clear message", async () => {
    mockFetch(new TypeError("Failed to fetch"));
    const error = (await api.get("/api/me").catch((e: unknown) => e)) as ApiError;
    expect(error.status).toBe(0);
    expect(error.message).toContain("Server nije dostupan");
  });
});
