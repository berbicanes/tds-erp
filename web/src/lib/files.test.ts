import { describe, expect, it } from "vitest";
import { checkFile, formatFileSize, MAX_FILE_BYTES } from "./files";

describe("checkFile (DATA_MODEL §14)", () => {
  it("accepts allowed extensions in any case", () => {
    expect(checkFile({ name: "Crtez.PDF", size: 10 })).toBeNull();
    expect(checkFile({ name: "dio.step", size: 10 })).toBeNull();
  });

  it("rejects other extensions, empty and too large files", () => {
    expect(checkFile({ name: "alat.exe", size: 10 })).toContain("nije dozvoljena");
    expect(checkFile({ name: "bez", size: 10 })).toContain("nije dozvoljena");
    expect(checkFile({ name: "a.pdf", size: 0 })).toBe("Fajl je prazan.");
    expect(checkFile({ name: "a.pdf", size: MAX_FILE_BYTES + 1 })).toBe("Fajl je veći od 25 MB.");
    expect(checkFile({ name: "a.pdf", size: MAX_FILE_BYTES })).toBeNull();
  });
});

describe("formatFileSize", () => {
  it("uses B, KB and MB with decimal comma", () => {
    expect(formatFileSize(512)).toBe("512 B");
    expect(formatFileSize(1536)).toBe("1,5 KB");
    expect(formatFileSize(2.5 * 1024 * 1024)).toBe("2,5 MB");
  });
});
