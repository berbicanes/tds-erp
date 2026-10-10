import { describe, expect, it } from "vitest";
import { itemTitle, qtyWithUnit } from "./material";

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
});
