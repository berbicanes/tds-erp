/** Today's business date in Europe/Sarajevo as "YYYY-MM-DD" (for <input type="date"> defaults and max). */
export function todaySarajevo(now: Date = new Date()): string {
  return new Intl.DateTimeFormat("en-CA", { timeZone: "Europe/Sarajevo", year: "numeric", month: "2-digit", day: "2-digit" }).format(now);
}
