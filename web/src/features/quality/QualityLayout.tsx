import { NavLink, Outlet } from "react-router";
import { PageHeader } from "../../components/PageHeader";

const tab = ({ isActive }: { isActive: boolean }) =>
  `flex min-h-11 items-center border-b-2 px-3 text-sm ${isActive ? "border-blue-700 font-semibold text-blue-800" : "border-transparent text-slate-600"}`;

/** UI §2: Kvalitet = kontrole + neusaglašenosti. */
export function QualityLayout() {
  return (
    <>
      <PageHeader title="Kvalitet" />
      <nav aria-label="Kvalitet" className="flex gap-1 border-b border-slate-200 px-4">
        <NavLink to="/kvalitet/kontrole" className={tab}>Kontrole</NavLink>
        <NavLink to="/kvalitet/nc" className={tab}>Neusaglašenosti</NavLink>
      </nav>
      <Outlet />
    </>
  );
}
