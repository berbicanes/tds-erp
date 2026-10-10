import { useId, useRef, useState } from "react";
import { ALLOWED_EXTENSIONS, checkFile, formatFileSize } from "../../lib/files";

/** UI §7: drag & drop + button; shows name and size; can be removed before saving. Upload happens on save. */
export function FilePicker({ file, onChange, label = "Odaberi fajl" }: {
  file: File | null;
  onChange: (file: File | null) => void;
  label?: string;
}) {
  const id = useId();
  const input = useRef<HTMLInputElement>(null);
  const [over, setOver] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const take = (f: File | undefined) => {
    if (!f) return;
    const problem = checkFile(f);
    setError(problem);
    onChange(problem ? null : f);
  };

  return (
    <div className="space-y-1">
      {file ? (
        <div className="flex flex-wrap items-center gap-3 rounded border border-slate-300 px-3 py-2 text-sm">
          <span className="font-medium break-all">{file.name}</span>
          <span className="text-slate-500 tabular-nums">{formatFileSize(file.size)}</span>
          <button type="button" onClick={() => { onChange(null); if (input.current) input.current.value = ""; }}
            className="ml-auto min-h-9 rounded border border-slate-300 px-2">Ukloni</button>
        </div>
      ) : (
        <label
          htmlFor={id}
          onDragOver={(e) => { e.preventDefault(); setOver(true); }}
          onDragLeave={() => setOver(false)}
          onDrop={(e) => { e.preventDefault(); setOver(false); take(e.dataTransfer.files[0]); }}
          className={`flex min-h-16 cursor-pointer flex-col items-center justify-center rounded border-2 border-dashed px-3 py-3 text-center text-sm ${over ? "border-blue-600 bg-blue-50" : "border-slate-300"}`}
        >
          <span className="font-medium text-blue-800">{label}</span>
          <span className="text-xs text-slate-500">ili prevuci fajl ovdje · do 25 MB · {ALLOWED_EXTENSIONS.join(" ")}</span>
        </label>
      )}
      <input ref={input} id={id} type="file" className="sr-only" accept={ALLOWED_EXTENSIONS.join(",")} onChange={(e) => take(e.target.files?.[0])} />
      {error && <p className="text-sm text-red-700">{error}</p>}
    </div>
  );
}
