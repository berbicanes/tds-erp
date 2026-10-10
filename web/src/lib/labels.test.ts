import { describe, expect, it } from "vitest";
import dataModel from "../../../docs/DATA_MODEL.md?raw";
import theme from "tailwindcss/theme.css?raw";
import { enumLabels, labelOf, toneClasses, userLabel, workOrderStatus, type Tone } from "./labels";

/** Parses the "Enumi — kompletna lista" block of DATA_MODEL.md. */
function enumsFromDataModel(): Record<string, string[]> {
  const block = /## Enumi[\s\S]*?```\n([\s\S]*?)```/.exec(dataModel)?.[1];
  if (!block) throw new Error("Enum block not found in DATA_MODEL.md");
  const result: Record<string, string[]> = {};
  let current = "";
  for (const line of block.split("\n")) {
    const header = /^(\w+):\s*(.*)$/.exec(line);
    const values = header ? header[2]! : line;
    if (header) current = header[1]!;
    if (!current) continue;
    result[current] = [...(result[current] ?? []), ...values.split(",").map((v) => v.trim()).filter(Boolean)];
  }
  return result;
}

const camel = (pascal: string) => pascal[0]!.toLowerCase() + pascal.slice(1);

describe("labels.ts matches DATA_MODEL.md enums", () => {
  const model = enumsFromDataModel();

  it("covers every enum", () => {
    expect(Object.keys(enumLabels).sort()).toEqual(Object.keys(model).map(camel).sort());
  });

  it.each(Object.entries(model))("%s has exactly the DATA_MODEL values", (name, values) => {
    const map = enumLabels[camel(name) as keyof typeof enumLabels];
    expect(Object.keys(map)).toEqual(values);
  });
});

describe("display text is 1:1 from NOTION_SOURCE.md", () => {
  it.each([
    [enumLabels.workOrderStatus, ["Zaprimljen", "Čeka materijal", "Spreman", "Laser", "Presanje", "Kontrola", "Doraditi", "Završeno", "Isporučeno", "Otkazano"]],
    [enumLabels.qualityApproval, ["Čeka", "Prihvaćeno", "Blokirano"]],
    [enumLabels.supplierApprovalStatus, ["U procjeni", "Odobren", "Uslovno odobren", "Blokiran"]],
    [enumLabels.receiptDecision, ["Na kontroli", "Prihvaćen", "Karantin", "Odbijen"]],
    [enumLabels.unit, ["kg", "kom", "m", "tabla", "drugo"]],
    [enumLabels.inspectionPhase, ["Ulazni materijal", "Lasersko rezanje", "Hidraulično presanje", "Završna kontrola"]],
    [enumLabels.inspectionResult, ["Čeka", "Prihvaćeno", "Neusaglašeno"]],
    [enumLabels.ncSource, ["Dobavljač", "Ulazni materijal", "Laser", "Presanje", "Završna kontrola", "Kupac", "Audit", "Ostalo"]],
    [enumLabels.productDecision, ["Čeka odluku", "Dorada", "Škart", "Povrat dobavljaču", "Odstupanje odobreno"]],
    [enumLabels.ncStatus, ["Otvorena", "Radnja u toku", "Provjera efektivnosti", "Zatvorena"]],
    [enumLabels.documentKind, ["Procedura", "Radno uputstvo", "Obrazac / zapis", "Eksterni dokument"]],
    [enumLabels.documentStatus, ["Nacrt", "Na pregledu", "Odobren", "Povučen"]],
  ])("%#", (map, texts) => {
    expect(Object.values(map).map((x) => x.text)).toEqual(texts);
  });
});

describe("tones follow UI §8", () => {
  const tones = (map: Record<string, { tone: Tone }>) => Object.fromEntries(Object.entries(map).map(([k, v]) => [k, v.tone]));

  it("work order status", () => {
    expect(tones(enumLabels.workOrderStatus)).toEqual({
      Zaprimljen: "slate", CekaMaterijal: "amber", Spreman: "blue", Laser: "blue", Presanje: "blue",
      Kontrola: "blue", Doraditi: "amber", Zavrseno: "green", Isporuceno: "zinc", Otkazano: "zinc",
    });
  });

  it("Hitno is amber, Normalan slate", () => {
    expect(tones(enumLabels.priority)).toEqual({ Normalan: "slate", Hitno: "amber" });
  });

  it("problems are red", () => {
    expect(enumLabels.qualityApproval.Blokirano.tone).toBe("red");
    expect(enumLabels.supplierApprovalStatus.Blokiran.tone).toBe("red");
    expect(enumLabels.receiptDecision.Karantin.tone).toBe("red");
    expect(enumLabels.receiptDecision.Odbijen.tone).toBe("red");
    expect(enumLabels.inspectionResult.Neuskladjeno.tone).toBe("red");
    expect(enumLabels.ncStatus.Otvorena.tone).toBe("red");
  });

  it("values not listed in UI §8 are slate", () => {
    for (const map of [enumLabels.unit, enumLabels.stockMovementType, enumLabels.equipmentKind, enumLabels.inspectionPhase,
      enumLabels.ncSource, enumLabels.productDecision, enumLabels.documentKind]) {
      expect(new Set(Object.values(map).map((x) => x.tone))).toEqual(new Set(["slate"]));
    }
  });
});

describe("badge contrast ≥ 4.5:1 (UI §8)", () => {
  function oklchToLuminance(l: number, c: number, hDeg: number): number {
    const h = (hDeg * Math.PI) / 180;
    const a = c * Math.cos(h);
    const b = c * Math.sin(h);
    const l_ = (l + 0.3963377774 * a + 0.2158037573 * b) ** 3;
    const m_ = (l - 0.1055613458 * a - 0.0638541728 * b) ** 3;
    const s_ = (l - 0.0894841775 * a - 1.291485548 * b) ** 3;
    const clamp = (x: number) => Math.min(1, Math.max(0, x));
    const r = clamp(4.0767416621 * l_ - 3.3077115913 * m_ + 0.2309699292 * s_);
    const g = clamp(-1.2684380046 * l_ + 2.6097574011 * m_ - 0.3413193965 * s_);
    const bl = clamp(-0.0041960863 * l_ - 0.7034186147 * m_ + 1.707614701 * s_);
    return 0.2126 * r + 0.7152 * g + 0.0722 * bl;
  }

  function luminance(token: string): number {
    const m = new RegExp(`--color-${token}:\\s*oklch\\(([\\d.]+)%\\s+([\\d.]+)\\s+([\\d.]+)\\)`).exec(theme);
    if (!m) throw new Error(`Color ${token} not found in tailwind theme`);
    return oklchToLuminance(Number(m[1]) / 100, Number(m[2]), Number(m[3]));
  }

  it.each(Object.entries(toneClasses))("%s", (_, classes) => {
    const bg = /bg-([a-z]+-\d+)/.exec(classes)![1]!;
    const fg = /text-([a-z]+-\d+)/.exec(classes)![1]!;
    const [hi, lo] = [luminance(bg), luminance(fg)].sort((x, y) => y - x) as [number, number];
    expect((hi + 0.05) / (lo + 0.05)).toBeGreaterThanOrEqual(4.5);
  });
});

describe("labelOf / userLabel", () => {
  it("returns the label", () => {
    expect(labelOf(workOrderStatus, "CekaMaterijal")).toEqual({ text: "Čeka materijal", tone: "amber" });
  });

  it("shows unknown values as-is and null as —", () => {
    expect(labelOf(workOrderStatus, "NovaVrijednost")).toEqual({ text: "NovaVrijednost", tone: "slate" });
    expect(labelOf(workOrderStatus, null).text).toBe("—");
  });

  it("null user is Sistem", () => {
    expect(userLabel(null)).toBe("Sistem");
    expect(userLabel("anes")).toBe("anes");
  });
});
