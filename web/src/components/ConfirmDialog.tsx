import { useEffect, useRef, type ReactNode } from "react";

interface Props {
  open: boolean;
  title: string;
  children?: ReactNode;
  confirmText: string;
  cancelText?: string;
  danger?: boolean;
  onConfirm: () => void;
  onCancel: () => void;
}

/** Native <dialog> modal (no browser confirm()). */
export function ConfirmDialog({ open, title, children, confirmText, cancelText = "Odustani", danger, onConfirm, onCancel }: Props) {
  const ref = useRef<HTMLDialogElement>(null);
  useEffect(() => {
    const d = ref.current;
    if (!d) return;
    if (open && !d.open) d.showModal();
    if (!open && d.open) d.close();
  }, [open]);

  return (
    <dialog
      ref={ref}
      onCancel={(e) => {
        e.preventDefault();
        onCancel();
      }}
      className="m-auto w-[min(28rem,calc(100vw-2rem))] rounded border border-slate-200 p-0 backdrop:bg-slate-900/40"
    >
      <div className="space-y-3 p-4">
        <h2 className="font-semibold">{title}</h2>
        {children && <div className="text-sm text-slate-700">{children}</div>}
        <div className="flex justify-end gap-2 pt-2">
          <button type="button" onClick={onCancel} className="min-h-12 rounded border border-slate-300 px-4">
            {cancelText}
          </button>
          <button
            type="button"
            onClick={onConfirm}
            className={`min-h-12 rounded px-4 font-medium text-white ${danger ? "bg-red-700" : "bg-blue-700"}`}
          >
            {confirmText}
          </button>
        </div>
      </div>
    </dialog>
  );
}
