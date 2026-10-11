import { describe, expect, it } from "vitest";
import { incompleteLabel, itemTitle, qtyWithUnit } from "./material";

describe("material helpers", () => {
  it("itemTitle joins name, grade and thickness", () => {
    expect(itemTitle({ name: "Lim crni", grade: "S235", thicknessMm: 2.5 })).toBe("Lim crni S235 2,5 mm");
    expect(itemTitle({ name: "Cijev", grade: null, thicknessMm: null })).toBe("Cijev");
  });

  it("qtyWithUnit", () => {
    expect(qtyWithUnit(1234.5, "kg")).toBe("1.234,5 kg");
    expect(qtyWithUnit(null, "kg")).toBe("—");
    expect(qtyWithUnit(0, "tabla")).toBe("0 tabla");
  });

  it("incomplete label: none when all stock is known, Bosnian plural otherwise (DOMAIN §2.6)", () => {
    expect(incompleteLabel(0)).toBeNull();
    expect(incompleteLabel(1)).toBe("Nepotpuno: 1 lot bez količine");
    expect(incompleteLabel(3)).toBe("Nepotpuno: 3 lota bez količine");
    expect(incompleteLabel(5)).toBe("Nepotpuno: 5 lotova bez količine");
    expect(incompleteLabel(12)).toBe("Nepotpuno: 12 lotova bez količine");
    expect(incompleteLabel(21)).toBe("Nepotpuno: 21 lot bez količine");
  });
});
