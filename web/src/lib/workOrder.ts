import { todaySarajevo } from "./date";

/** UI §3: due date red when overdue, amber when ≤ 3 days away (active orders only). */
export function dueTone(dueDate: string, isOverdue: boolean, isFinished: boolean, today = todaySarajevo()): "red" | "amber" | null {
  if (isFinished) return null;
  if (isOverdue) return "red";
  const days = (Date.parse(dueDate) - Date.parse(today)) / 86_400_000;
  return days <= 3 ? "amber" : null;
}

export const FINISHED = ["Zavrseno", "Isporuceno", "Otkazano"] as const;
export const isFinished = (status: string) => (FINISHED as readonly string[]).includes(status);
