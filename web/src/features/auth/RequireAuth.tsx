import { Navigate, Outlet, useLocation } from "react-router";
import { useMe } from "../../lib/auth";

/** Protected routes: not signed in → /prijava?next=… */
export function RequireAuth() {
  const me = useMe();
  const location = useLocation();

  if (me.isPending) {
    return <p className="p-4 text-slate-600">Učitavanje…</p>;
  }
  if (me.isError) {
    return <p className="p-4 text-red-700">{me.error.message}</p>;
  }
  if (!me.data) {
    const next = encodeURIComponent(location.pathname + location.search);
    return <Navigate to={`/prijava?next=${next}`} replace />;
  }
  return <Outlet />;
}
