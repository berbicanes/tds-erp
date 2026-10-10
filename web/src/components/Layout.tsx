import { useEffect, useState } from "react";
import { NavLink, Outlet, useLocation } from "react-router";
import { hasRole, useLogout, useMe } from "../lib/auth";
import { bottomNav, mainNav, type NavItem } from "./nav";

function visible(items: NavItem[], me: ReturnType<typeof useMe>["data"]) {
  return items.filter((i) => !i.roles || hasRole(me, ...i.roles));
}

const sideLink = ({ isActive }: { isActive: boolean }) =>
  `flex min-h-12 items-center rounded px-3 text-sm ${isActive ? "bg-blue-50 font-semibold text-blue-800" : "text-slate-700 hover:bg-slate-100"}`;

const bottomLink = ({ isActive }: { isActive: boolean }) =>
  `flex min-h-14 flex-1 items-center justify-center px-1 text-center text-xs ${isActive ? "font-semibold text-blue-800" : "text-slate-600"}`;

export function Layout() {
  const me = useMe();
  const logout = useLogout();
  const location = useLocation();
  const [menuOpen, setMenuOpen] = useState(false);
  const items = visible(mainNav, me.data);

  // Close the mobile menu after navigating.
  useEffect(() => setMenuOpen(false), [location.pathname]);

  const sideNav = (
    <nav aria-label="Glavni meni" className="space-y-1">
      {items.map((item) => (
        <NavLink key={item.to} to={item.to} end={item.to === "/"} className={sideLink}>
          {item.label}
        </NavLink>
      ))}
    </nav>
  );

  const userBox = (
    <div className="space-y-2 border-t border-slate-200 pt-3 text-sm">
      <p className="truncate px-3 text-slate-600">{me.data?.userName}</p>
      <button
        type="button"
        onClick={() => logout.mutate()}
        disabled={logout.isPending}
        className="flex min-h-12 w-full items-center rounded px-3 text-left text-slate-700 hover:bg-slate-100"
      >
        Odjavi se
      </button>
    </div>
  );

  return (
    <div className="min-h-dvh bg-white md:flex">
      {/* Desktop sidebar */}
      <aside className="hidden w-56 shrink-0 flex-col justify-between border-r border-slate-200 bg-slate-50 p-3 md:flex md:min-h-dvh">
        <div className="space-y-4">
          <p className="px-3 pt-1 font-semibold">TDS ERP</p>
          {sideNav}
        </div>
        {userBox}
      </aside>

      {/* Mobile header + slide-down menu with all modules */}
      <header className="sticky top-0 z-20 flex h-14 items-center justify-between border-b border-slate-200 bg-white px-4 md:hidden">
        <span className="font-semibold">TDS ERP</span>
        <button
          type="button"
          aria-expanded={menuOpen}
          aria-controls="mobilni-meni"
          onClick={() => setMenuOpen((o) => !o)}
          className="flex min-h-12 min-w-12 items-center justify-center rounded px-3 text-sm text-slate-700 hover:bg-slate-100"
        >
          {menuOpen ? "Zatvori" : "Meni"}
        </button>
      </header>
      {menuOpen && (
        <div id="mobilni-meni" className="fixed inset-x-0 top-14 bottom-14 z-20 overflow-y-auto bg-white p-3 md:hidden">
          {sideNav}
          <div className="mt-4">{userBox}</div>
        </div>
      )}

      <main className="min-w-0 flex-1 pb-16 md:pb-0">
        <Outlet />
      </main>

      {/* Mobile bottom bar (UI §2) */}
      <nav
        aria-label="Brza navigacija"
        className="fixed inset-x-0 bottom-0 z-20 flex border-t border-slate-200 bg-white pb-[env(safe-area-inset-bottom)] md:hidden"
      >
        {bottomNav.map((item) => (
          <NavLink key={item.to} to={item.to} end={item.to === "/"} className={bottomLink}>
            {item.label}
          </NavLink>
        ))}
      </nav>
    </div>
  );
}
