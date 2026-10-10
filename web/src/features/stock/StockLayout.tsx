import { NavLink, Outlet } from "react-router";
import { PageHeader } from "../../components/PageHeader";

const tab = ({ isActive }: { isActive: boolean }) =>
  `flex min-h-11 items-center border-b-2 px-3 text-sm ${isActive ? "border-blue-700 font-semibold text-blue-800" : "border-transparent text-slate-600"}`;

/** UI §2: Zalihe = artikli + lotovi + pokreti. */
export function StockLayout() {
  return (
    <>
      <PageHeader title="Zalihe" />
      <nav aria-label="Zalihe" className="flex gap-1 border-b border-slate-200 px-4">
        <NavLink to="/zalihe/artikli" className={tab}>Artikli</NavLink>
        <NavLink to="/zalihe/lotovi" className={tab}>Lotovi</NavLink>
      </nav>
      <Outlet />
    </>
  );
}
