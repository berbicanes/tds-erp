import { describe, expect, it } from "vitest";
import { nextSort, readListState, sortDirection, toApiQuery, toggleFilter, writeListState } from "./listParams";

describe("list state in the URL", () => {
  it("reads page, sort, search and repeated filters", () => {
    const s = readListState(new URLSearchParams("page=3&sort=-dueDate&q=lim&status=Laser&status=Presanje&priority=Hitno"));
    expect(s).toEqual({
      page: 3,
      sort: "-dueDate",
      q: "lim",
      filters: { status: ["Laser", "Presanje"], priority: ["Hitno"] },
    });
  });

  it("defaults for an empty or invalid URL", () => {
    expect(readListState(new URLSearchParams("page=abc&status="))).toEqual({ page: 1, sort: null, q: "", filters: {} });
    expect(readListState(new URLSearchParams("page=-2")).page).toBe(1);
  });

  it("round-trips and omits defaults", () => {
    const url = "page=2&q=inox&sort=number&status=Laser";
    expect(writeListState(readListState(new URLSearchParams(url))).toString()).toBe("page=2&sort=number&q=inox&status=Laser");
    expect(writeListState({ page: 1, sort: null, q: "  ", filters: {} }).toString()).toBe("");
  });

  it("api query always has page and pageSize", () => {
    expect(toApiQuery({ page: 1, sort: null, q: "", filters: {} })).toBe("page=1&pageSize=50");
  });
});

describe("sorting", () => {
  it("toggles ascending ↔ descending on the same column", () => {
    expect(nextSort(null, "dueDate")).toBe("dueDate");
    expect(nextSort("dueDate", "dueDate")).toBe("-dueDate");
    expect(nextSort("-dueDate", "dueDate")).toBe("dueDate");
    expect(nextSort("-dueDate", "number")).toBe("number");
  });

  it("reports direction per column", () => {
    expect(sortDirection("dueDate", "dueDate")).toBe("asc");
    expect(sortDirection("-dueDate", "dueDate")).toBe("desc");
    expect(sortDirection("-dueDate", "number")).toBeNull();
  });
});

describe("filter chips", () => {
  const base = { page: 4, sort: null, q: "", filters: { status: ["Laser"] } };

  it("adds and removes values and resets to page 1", () => {
    const added = toggleFilter(base, "status", "Presanje");
    expect(added.filters).toEqual({ status: ["Laser", "Presanje"] });
    expect(added.page).toBe(1);
    expect(toggleFilter(base, "status", "Laser").filters).toEqual({});
  });
});
