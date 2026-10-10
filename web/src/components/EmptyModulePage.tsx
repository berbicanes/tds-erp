import { PageHeader } from "./PageHeader";

/** Placeholder until the module is built in a later phase (ROADMAP). */
export function EmptyModulePage({ title }: { title: string }) {
  return (
    <>
      <PageHeader title={title} />
      <p className="px-4 py-6 text-slate-600">Ovaj dio još nije napravljen.</p>
    </>
  );
}
