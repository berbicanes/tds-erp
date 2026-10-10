// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { act, cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createMemoryRouter, RouterProvider } from "react-router";
import { EmployeeFormPage } from "./EmployeeFormPage";

/**
 * Regression test for the bug found after Phase 1: a refetch (e.g. on window focus) with newer server data
 * overwrote unsaved input and moved the version, so the next save silently overwrote someone else's change.
 */

interface Employee { id: string; fullName: string; position: string | null; isActive: boolean; userId: string | null; userName: string | null; version: number }

const v1: Employee = { id: "e1", fullName: "Marko Marić", position: "Operater lasera", isActive: true, userId: null, userName: null, version: 1 };
const v2: Employee = { ...v1, position: "Vođa smjene", version: 2 };

function setup() {
  let server: Employee = v1;
  const puts: { version: number; position: string | null }[] = [];

  const json = (body: unknown, status = 200) =>
    new Response(JSON.stringify(body), { status, headers: { "Content-Type": status >= 400 ? "application/problem+json" : "application/json" } });

  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url === "/api/employees/e1" && (!init?.method || init.method === "GET")) return json(server);
    if (url === "/api/employees/e1" && init?.method === "PUT") {
      const body = JSON.parse(String(init.body)) as { version: number; position: string | null };
      puts.push(body);
      if (body.version !== server.version)
        return json({ status: 409, detail: "Zapis je u međuvremenu izmijenjen. Osvježi i pokušaj ponovo.", code: "concurrency" }, 409);
      server = { ...server, position: body.position, version: server.version + 1 };
      return json(server);
    }
    if (url === "/api/users") return json([]);
    if (url === "/api/me") return json({ id: "u1", userName: "admin", roles: ["Admin"] });
    return json({ detail: "not mocked " + url }, 404);
  }));

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false, staleTime: 0 } } });
  const router = createMemoryRouter(
    [
      { path: "/podesavanja/zaposlenici/:id", element: <EmployeeFormPage /> },
      { path: "/podesavanja/zaposlenici", element: <p>Lista zaposlenika</p> },
    ],
    { initialEntries: ["/podesavanja/zaposlenici/e1"] },
  );
  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );

  return {
    queryClient,
    puts,
    serverChangesTo: (e: Employee) => { server = e; },
    /** What a window-focus refetch does. */
    refetch: () => act(() => queryClient.refetchQueries({ queryKey: ["employees", "e1"] })),
    position: () => screen.getByLabelText(/Radno mjesto/) as HTMLInputElement,
  };
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
});

describe("EmployeeFormPage — unsaved changes vs. refetch", () => {
  it("keeps unsaved input when a refetch brings newer data, saves with the original version and gets a 409", async () => {
    const t = setup();
    await waitFor(() => expect(t.position().value).toBe("Operater lasera"));

    fireEvent.input(t.position(), { target: { value: "Operater lasera i plazme" } });
    t.serverChangesTo(v2); // someone else saves
    await t.refetch();

    // Unsaved input survives and the user is told.
    expect(t.position().value).toBe("Operater lasera i plazme");
    expect(await screen.findByText(/u međuvremenu izmijenio neko drugi/)).toBeTruthy();

    // Save sends the version the user started from → conflict, nothing overwritten.
    fireEvent.click(screen.getByRole("button", { name: "Spremi" }));
    await waitFor(() => expect(t.puts).toHaveLength(1));
    expect(t.puts[0]!.version).toBe(1);
    expect(await screen.findByText("Zapis je u međuvremenu izmijenjen. Osvježi i pokušaj ponovo.")).toBeTruthy();

    // "Osvježi" loads the newest data.
    fireEvent.click(screen.getByRole("button", { name: "Osvježi" }));
    await waitFor(() => expect(t.position().value).toBe("Vođa smjene"));
  });

  it("a clean form takes newer server data on refetch", async () => {
    const t = setup();
    await waitFor(() => expect(t.position().value).toBe("Operater lasera"));

    t.serverChangesTo(v2);
    await t.refetch();

    await waitFor(() => expect(t.position().value).toBe("Vođa smjene"));
    expect(screen.queryByText(/u međuvremenu izmijenio neko drugi/)).toBeNull();
  });

  it("a refetch with the same version does not touch unsaved input", async () => {
    const t = setup();
    await waitFor(() => expect(t.position().value).toBe("Operater lasera"));

    fireEvent.input(t.position(), { target: { value: "Moja izmjena" } });
    await t.refetch();

    expect(t.position().value).toBe("Moja izmjena");
    expect(screen.queryByText(/u međuvremenu izmijenio neko drugi/)).toBeNull();
  });
});
