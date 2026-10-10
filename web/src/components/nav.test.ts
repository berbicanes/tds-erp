import { describe, expect, it } from "vitest";
import { bottomNav, mainNav } from "./nav";

describe("navigation (UI §2)", () => {
  it("sidebar has all modules in order", () => {
    expect(mainNav.map((i) => i.label)).toEqual([
      "Pregled", "Nalozi", "Dnevna proizvodnja", "Zalihe", "Kvalitet", "Dobavljači", "Oprema", "Dokumenti", "Podešavanja",
    ]);
  });

  it("Podešavanja is Admin only", () => {
    expect(mainNav.find((i) => i.label === "Podešavanja")?.roles).toEqual(["Admin"]);
  });

  it("mobile bottom bar has exactly 4 items", () => {
    expect(bottomNav.map((i) => i.label)).toEqual(["Pregled", "Brzi unos", "Nalozi", "Zalihe"]);
  });
});
