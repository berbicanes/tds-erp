/** @vitest-environment jsdom */
import { cleanup, render } from "@testing-library/react";
import { afterEach, describe, expect, it } from "vitest";
import { LevelWithIncomplete, levelText } from "./LevelWithIncomplete";

afterEach(cleanup);

describe("stock level of an item with lots without quantity (DOMAIN §2.4, §2.6)", () => {
  it("known level OK stays OK next to the Nepotpuno mark", () => {
    const { container } = render(<LevelWithIncomplete item={{ stockLevel: "Ok", unknownLotCount: 1 }} />);
    expect(container.textContent).toBe("OKNepotpuno: 1 lot bez količine");
    expect(levelText({ stockLevel: "Ok", unknownLotCount: 1 })).toBe("OK · Nepotpuno: 1 lot bez količine");
  });

  it("unknown level is a dash, never Nema", () => {
    const { container } = render(<LevelWithIncomplete item={{ stockLevel: null, unknownLotCount: 2 }} />);
    expect(container.textContent).toBe("—Nepotpuno: 2 lota bez količine");
    expect(levelText({ stockLevel: null, unknownLotCount: 2 })).toBe("— · Nepotpuno: 2 lota bez količine");
  });
});
