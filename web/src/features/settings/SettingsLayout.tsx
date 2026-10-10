import { NavLink, Outlet } from "react-router";
import { PageHeader } from "../../components/PageHeader";

const tab = ({ isActive }: { isActive: boolean }) =>
  `flex min-h-11 items-center border-b-2 px-3 text-sm ${isActive ? "border-blue-700 font-semibold text-blue-800" : "border-transparent text-slate-600"}`;

export function SettingsLayout() {
  return (
    <>
      <PageHeader title="Podešavanja" />
      <nav aria-label="Podešavanja" className="flex gap-1 border-b border-slate-200 px-4">
        <NavLink to="/podesavanja/zaposlenici" className={tab}>Zaposlenici</NavLink>
        <NavLink to="/podesavanja/korisnici" className={tab}>Korisnici</NavLink>
      </nav>
      <Outlet />
    </>
  );
}
