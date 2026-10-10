import { describe, expect, it } from "vitest";
import { hasRole, safeNext } from "./auth";

describe("safeNext", () => {
  it.each([
    ["/nalozi?status=Laser", "/nalozi?status=Laser"],
    ["/", "/"],
  ])("keeps in-app path %s", (input, expected) => {
    expect(safeNext(input)).toBe(expected);
  });

  it.each([null, "", "https://evil.example", "//evil.example", "/\\evil.example", "nalozi"])(
    "falls back to / for %s",
    (input) => {
      expect(safeNext(input)).toBe("/");
    },
  );
});

describe("hasRole", () => {
  const kontrolor = { id: "1", userName: "k", roles: ["Kontrolor" as const] };

  it("is true when the user has one of the roles", () => {
    expect(hasRole(kontrolor, "Admin", "Kontrolor")).toBe(true);
  });

  it("is false for other roles or no user", () => {
    expect(hasRole(kontrolor, "Admin")).toBe(false);
    expect(hasRole(null, "Admin")).toBe(false);
  });
});
