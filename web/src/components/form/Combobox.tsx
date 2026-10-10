import { useId, useMemo, useRef, useState } from "react";
import { inputClass } from "./Field";

export interface Option {
  value: string;
  label: string;
  /** Extra searchable/visible text, e.g. customer name. */
  detail?: string;
}

/** Searchable select (UI §7) for employees, lots, suppliers. Keyboard: ↑ ↓ Enter Esc. */
export function Combobox({ id, options, value, onChange, placeholder, invalid, allowEmpty, emptyLabel = "—" }: {
  id: string;
  options: Option[];
  value: string | null;
  onChange: (value: string | null) => void;
  placeholder?: string;
  invalid?: boolean;
  allowEmpty?: boolean;
  emptyLabel?: string;
}) {
  const listId = useId();
  const [open, setOpen] = useState(false);
  const [text, setText] = useState("");
  const [active, setActive] = useState(0);
  const blurTimer = useRef<ReturnType<typeof setTimeout>>(undefined);

  const selected = options.find((o) => o.value === value) ?? null;
  const filtered = useMemo(() => {
    const t = text.trim().toLocaleLowerCase("bs");
    const list = t
      ? options.filter((o) => `${o.label} ${o.detail ?? ""}`.toLocaleLowerCase("bs").includes(t))
      : options;
    return allowEmpty ? [{ value: "", label: emptyLabel }, ...list] : list;
  }, [options, text, allowEmpty, emptyLabel]);

  const choose = (o: Option | undefined) => {
    if (!o) return;
    onChange(o.value === "" ? null : o.value);
    setText("");
    setOpen(false);
  };

  return (
    <div className="relative">
      <input
        id={id}
        role="combobox"
        aria-expanded={open}
        aria-controls={listId}
        aria-autocomplete="list"
        aria-invalid={invalid}
        autoComplete="off"
        className={inputClass}
        placeholder={selected ? selected.label : placeholder ?? "Odaberi…"}
        value={open ? text : selected ? `${selected.label}${selected.detail ? ` — ${selected.detail}` : ""}` : ""}
        onFocus={() => {
          setOpen(true);
          setActive(0);
        }}
        onBlur={() => {
          blurTimer.current = setTimeout(() => setOpen(false), 150);
        }}
        onChange={(e) => {
          setText(e.target.value);
          setActive(0);
          setOpen(true);
        }}
        onKeyDown={(e) => {
          if (e.key === "ArrowDown") {
            e.preventDefault();
            setActive((a) => Math.min(a + 1, filtered.length - 1));
          } else if (e.key === "ArrowUp") {
            e.preventDefault();
            setActive((a) => Math.max(a - 1, 0));
          } else if (e.key === "Enter" && open) {
            e.preventDefault();
            choose(filtered[active]);
          } else if (e.key === "Escape") {
            setOpen(false);
          }
        }}
      />
      {open && (
        <ul
          id={listId}
          role="listbox"
          className="absolute z-30 mt-1 max-h-64 w-full overflow-y-auto rounded border border-slate-200 bg-white text-sm shadow"
          onMouseDown={() => clearTimeout(blurTimer.current)}
        >
          {filtered.length === 0 && <li className="px-3 py-2 text-slate-500">Nema rezultata.</li>}
          {filtered.map((o, i) => (
            <li
              key={o.value || "__empty"}
              role="option"
              aria-selected={o.value === (value ?? "")}
              className={`cursor-pointer px-3 py-2 ${i === active ? "bg-blue-50" : ""}`}
              onMouseEnter={() => setActive(i)}
              onClick={() => choose(o)}
            >
              <span className="font-medium">{o.label}</span>
              {o.detail && <span className="text-slate-500"> — {o.detail}</span>}
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
