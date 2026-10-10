import { Outlet } from "react-router";
import { hasRole, useMe, type Role } from "../../lib/auth";

export function RequireRole({ roles }: { roles: Role[] }) {
  const me = useMe();
  if (!hasRole(me.data, ...roles)) {
    return <p className="px-4 py-6 text-slate-700">Nemaš ovlasti za ovu stranicu.</p>;
  }
  return <Outlet />;
}
