// @vitest-environment jsdom
import { useState } from "react";
import { afterEach, describe, expect, it } from "vitest";
import { cleanup, fireEvent, render, screen } from "@testing-library/react";
import { Combobox } from "./Combobox";

afterEach(cleanup);

function Harness() {
  const [value, setValue] = useState<string | null>(null);
  return (
    <>
      <Combobox id="c" value={value} onChange={setValue} allowEmpty emptyLabel="Bez mjernog sredstva"
        options={[{ value: "a", label: "MS-001 Pomično mjerilo" }, { value: "b", label: "MS-009 Mikrometar" }]} />
      <output data-testid="value">{value ?? "null"}</output>
    </>
  );
}

describe("Combobox with an empty choice", () => {
  it("Enter after typing picks the first match, not the empty choice", () => {
    render(<Harness />);
    const input = screen.getByRole("combobox");
    fireEvent.focus(input);
    fireEvent.change(input, { target: { value: "MS-009" } });
    fireEvent.keyDown(input, { key: "Enter" });
    expect(screen.getByTestId("value").textContent).toBe("b");
  });

  it("offers the empty choice when nothing is typed", () => {
    render(<Harness />);
    fireEvent.focus(screen.getByRole("combobox"));
    expect(screen.getAllByRole("option")[0]!.textContent).toBe("Bez mjernog sredstva");
  });
});
