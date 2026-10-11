import { describe, expect, it } from "vitest";
import { textOf } from "./DataTable";
import { Badge } from "./Badge";

describe("textOf (CSV fallback)", () => {
  it("reads strings, nested elements and badge labels", () => {
    expect(textOf("RN-1")).toBe("RN-1");
    expect(textOf(<span>RN-1<span> Metalbau</span></span>)).toBe("RN-1 Metalbau");
    expect(textOf(<Badge label={{ text: "Prihvaćeno", tone: "green" }} />)).toBe("Prihvaćeno");
    expect(textOf(null)).toBe("");
  });
});
