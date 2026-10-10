import { toneClasses, type Label } from "../lib/labels";

/** Status = colour + text, never colour alone (UI §1). */
export function Badge({ label }: { label: Label }) {
  return (
    <span className={`inline-block whitespace-nowrap rounded px-2 py-0.5 text-xs font-medium ${toneClasses[label.tone]}`}>
      {label.text}
    </span>
  );
}
