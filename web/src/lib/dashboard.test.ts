import { describe, expect, it } from "vitest";
import { kpiLinks, warningHref, warningText, type WarningLike } from "./dashboard";
import { readListState } from "./listParams";

const base: WarningLike = { kind: "", severity: "amber", entityId: "x1", title: "", detail: null, itemName: null, itemGrade: null, itemThicknessMm: null, date: null };
const today = "2026-10-12";

describe("dashboard warnings", () => {
  it.each([
    [{ kind: "LotKarantin", severity: "red", title: "AL-2291", itemName: "Lim aluminij", itemGrade: "AlMg3", itemThicknessMm: 4, date: "2026-10-08" }, "Lot AL-2291 (Lim aluminij AlMg3 4 mm) je u karantinu", "/zalihe/lotovi/x1"],
    [{ kind: "LotNaKontroli", title: "T-77940", itemName: "Lim inox", itemGrade: "1.4301", itemThicknessMm: 2.5 }, "Lot T-77940 (Lim inox 1.4301 2,5 mm) čeka ulaznu kontrolu", "/zalihe/lotovi/x1"],
    [{ kind: "OrderBlocked", severity: "red", title: "RN-2026-0007", detail: "Metalbau AG" }, "Nalog RN-2026-0007 (Metalbau AG) — kvalitet blokiran", "/nalozi/x1"],
    [{ kind: "Calibration", severity: "red", title: "MS-001", detail: "Pomično mjerilo", date: "2026-10-09" }, "Mjerno sredstvo MS-001 (Pomično mjerilo) je van kalibracije — kasni 3 dana (09.10.2026.)", "/oprema/x1"],
    [{ kind: "Calibration", title: "MS-002", detail: "Mikrometar", date: "2026-10-13" }, "Kalibracija MS-002 (Mikrometar) za 1 dan (13.10.2026.)", "/oprema/x1"],
    [{ kind: "SupplierEvaluation", title: "Metalia d.o.o.", date: "2026-11-01" }, "Ocjena dobavljača Metalia d.o.o. za 20 dana (01.11.2026.)", "/dobavljaci/x1"],
    [{ kind: "Stocktake", severity: "red", title: "Lim crni", itemName: "Lim crni", itemGrade: "S235", itemThicknessMm: 3, date: "2026-10-11" }, "Popis artikla Lim crni S235 3 mm — kasni 1 dan (11.10.2026.)", "/zalihe/artikli/x1"],
  ] as const)("%#", (partial, text, href) => {
    const w = { ...base, ...partial } as WarningLike;
    expect(warningText(w, today)).toBe(text);
    expect(warningHref(w)).toBe(href);
  });
});

describe("KPI links (UI §3: every number leads to its filtered list)", () => {
  const links = kpiLinks(today);
  it("orders and production", () => {
    expect(links.overdueOrders).toBe("/nalozi?overdue=true");
    expect(links.goodToday).toBe("/proizvodnja?from=2026-10-12&to=2026-10-12");
  });
  it("material tile filters exactly what it counts", () => {
    const state = readListState(new URL(links.itemsBelowMinimum, "http://x").searchParams);
    expect(state.filters).toEqual({ hasMinimum: ["true"], stockLevel: ["IspodMinimuma", "Nema"] });
  });
});
