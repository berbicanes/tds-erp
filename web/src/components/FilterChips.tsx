import type { Label } from "../lib/labels";
import { toggleFilter, type ListState } from "../lib/listParams";

/** Filter chips above a table (UI §5). Multi-select, stored in the URL. */
export function FilterChips({ name, options, state, onStateChange }: {
  name: string;
  options: Record<string, Label>;
  state: ListState;
  onStateChange: (next: ListState) => void;
}) {
  const selected = state.filters[name] ?? [];
  return (
    <div className="flex flex-wrap gap-1">
      {Object.entries(options).map(([value, label]) => {
        const on = selected.includes(value);
        return (
          <button
            key={value}
            type="button"
            aria-pressed={on}
            onClick={() => onStateChange(toggleFilter(state, name, value))}
            className={`min-h-8 rounded-full border px-3 text-xs ${on ? "border-blue-700 bg-blue-700 text-white" : "border-slate-300 bg-white text-slate-700"}`}
          >
            {label.text}
          </button>
        );
      })}
    </div>
  );
}
