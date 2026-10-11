// @vitest-environment jsdom
import { afterEach, describe, expect, it, vi } from "vitest";
import { cleanup, fireEvent, render, screen, waitFor } from "@testing-library/react";
import { QueryClient, QueryClientProvider } from "@tanstack/react-query";
import { createMemoryRouter, RouterProvider } from "react-router";
import { QuickEntryPage } from "./QuickEntryPage";

const order = { id: "w1", number: "RN-2026-0001", customer: "Metalbau AG", productDescription: "Nosač ugaoni", status: "Presanje" };
const closed = { id: "w2", number: "RN-2026-0009", customer: "Zatvoreni", productDescription: "X", status: "Zaprimljen" };

function setup({ finalPassed }: { finalPassed: boolean }) {
  const posts: Record<string, unknown>[] = [];
  const json = (body: unknown, status = 200) => new Response(JSON.stringify(body), { status, headers: { "Content-Type": "application/json" } });
  vi.stubGlobal("fetch", vi.fn(async (input: RequestInfo | URL, init?: RequestInit) => {
    const url = String(input);
    if (url === "/api/work-orders/options") return json([order, closed]);
    if (url === "/api/work-orders/w1") return json({ ...order, finalInspectionPassed: finalPassed });
    if (url === "/api/work-orders/w1/materials") return json([]);
    if (url === "/api/employees/options") return json([{ id: "e1", fullName: "Amra Hodžić" }]);
    if (url.startsWith("/api/equipment/options")) return json([{ id: "m1", code: "M-03", name: "Presa 350 t", kind: "Masina", calibrationStatus: null }]);
    if (url === "/api/production-entries" && init?.method === "POST") {
      posts.push(JSON.parse(String(init.body)) as Record<string, unknown>);
      return json({ data: { id: "p1", number: "DP-20261011-01" }, warnings: [] }, 201);
    }
    return json({ detail: "not mocked " + url }, 404);
  }));
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  const router = createMemoryRouter([{ path: "/brzi-unos", element: <QuickEntryPage /> }], { initialEntries: ["/brzi-unos"] });
  render(<QueryClientProvider client={queryClient}><RouterProvider router={router} /></QueryClientProvider>);
  return { posts };
}

afterEach(() => {
  cleanup();
  vi.unstubAllGlobals();
  vi.restoreAllMocks();
  try { window.localStorage.clear(); } catch { /* blocked in one test */ }
});

describe("QuickEntryPage (UI §4)", () => {
  it("works with blocked localStorage, lists only orders in production and disables Gotovo without final inspection", async () => {
    vi.spyOn(Storage.prototype, "getItem").mockImplementation(() => { throw new Error("SecurityError"); });
    vi.spyOn(Storage.prototype, "setItem").mockImplementation(() => { throw new Error("SecurityError"); });
    setup({ finalPassed: false });
    fireEvent.click(await screen.findByRole("button", { name: /RN-2026-0001/ }));
    expect(screen.queryByText("RN-2026-0009")).toBeNull();
    const good = await screen.findByLabelText("Gotovo ispravno");
    await waitFor(() => expect((good as HTMLInputElement).disabled).toBe(true));
    expect(screen.getByText("Gotovi ispravni komadi se upisuju tek nakon prihvaćene završne kontrole.")).toBeTruthy();
    expect(good.getAttribute("inputmode")).toBe("numeric");
  });

  it("saves with steppers, shows the toast and keeps order and operator for the next entry", async () => {
    const { posts } = setup({ finalPassed: true });
    fireEvent.click(await screen.findByRole("button", { name: /RN-2026-0001/ }));
    await screen.findByRole("option", { name: "Amra Hodžić" });
    fireEvent.change(screen.getByLabelText("2. Operater *"), { target: { value: "e1" } });
    fireEvent.click(await screen.findByRole("button", { name: "M-03" }));
    for (let i = 0; i < 3; i++) fireEvent.click(screen.getByRole("button", { name: "Presa — obrađeno: više" }));
    fireEvent.change(screen.getByLabelText("Gotovo ispravno"), { target: { value: "2" } });
    fireEvent.change(screen.getByLabelText("Radni sati *"), { target: { value: "1,5" } });
    fireEvent.click(screen.getByRole("button", { name: "Spremi" }));

    expect((await screen.findByRole("status")).textContent).toContain("Spremljeno DP-20261011-01");
    expect(posts[0]).toMatchObject({ workOrderId: "w1", operatorEmployeeId: "e1", equipmentIds: ["m1"], pressedQty: 3, goodQty: 2, laborHours: 1.5, laserCutQty: 0 });
    expect(screen.getByText(/RN-2026-0001 · Metalbau AG/)).toBeTruthy();
    expect((screen.getByLabelText("2. Operater *") as HTMLSelectElement).value).toBe("e1");
    expect((screen.getByLabelText("Presa — obrađeno") as HTMLInputElement).value).toBe("");
    expect(window.localStorage.getItem("tds.quick.operator")).toBe('"e1"');
    expect(window.localStorage.getItem("tds.quick.recentOrders")).toBe('["w1"]');
    expect((screen.getByLabelText("Radni sati *") as HTMLInputElement).value).toBe("1,5");
    expect(window.localStorage.getItem("tds.quick.hours")).toBe('"1,5"');
  });

  it("explains what is missing above the button instead of sending", async () => {
    const { posts } = setup({ finalPassed: true });
    fireEvent.click(await screen.findByRole("button", { name: /RN-2026-0001/ }));
    fireEvent.click(await screen.findByRole("button", { name: "Spremi" }));
    const alert = await screen.findByRole("alert");
    expect(alert.textContent).toContain("Odaberi operatera.");
    expect(alert.textContent).toContain("Unesi barem jednu količinu");
    expect(posts).toHaveLength(0);
  });

  it("prefills the last saved labor hours and ignores a stored value that is not a number", async () => {
    window.localStorage.setItem("tds.quick.hours", '"7,5"');
    setup({ finalPassed: true });
    fireEvent.click(await screen.findByRole("button", { name: /RN-2026-0001/ }));
    expect(((await screen.findByLabelText("Radni sati *")) as HTMLInputElement).value).toBe("7,5");
    cleanup();
    window.localStorage.setItem("tds.quick.hours", '"abc"');
    setup({ finalPassed: true });
    fireEvent.click(await screen.findByRole("button", { name: /RN-2026-0001/ }));
    expect(((await screen.findByLabelText("Radni sati *")) as HTMLInputElement).value).toBe("");
  });
});
