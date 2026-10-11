import { createBrowserRouter, Navigate, type RouteObject } from "react-router";
import { Layout } from "./components/Layout";
import { LoginPage } from "./features/auth/LoginPage";
import { RequireAuth } from "./features/auth/RequireAuth";
import { RequireRole } from "./features/auth/RequireRole";
import { NotFoundPage } from "./features/NotFoundPage";

/** Module screens load on first visit (keeps the first download small on phones in the hall). */
function page<M>(load: () => Promise<M>, pick: (m: M) => React.ComponentType): Pick<RouteObject, "lazy"> {
  return { lazy: async () => ({ Component: pick(await load()) }) };
}

const workOrders = () => import("./features/workOrders/pages");
const production = () => import("./features/production/pages");
const stock = () => import("./features/stock/pages");
const equipment = () => import("./features/equipment/pages");
const suppliers = () => import("./features/suppliers/pages");
const settings = () => import("./features/settings/pages");
const dashboard = () => import("./features/dashboard/pages");
const quality = () => import("./features/quality/pages");
const documents = () => import("./features/documents/pages");
const quickEntry = () => import("./features/quickEntry/pages");

export const router = createBrowserRouter([
  { path: "/prijava", element: <LoginPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <Layout />,
        children: [
          { path: "/", ...page(dashboard, (m) => m.DashboardPage) },
          { path: "/brzi-unos", ...page(quickEntry, (m) => m.QuickEntryPage) },
          { path: "/nalozi", ...page(workOrders, (m) => m.WorkOrderListPage) },
          { path: "/nalozi/novi", ...page(workOrders, (m) => m.WorkOrderFormPage) },
          { path: "/nalozi/:id", ...page(workOrders, (m) => m.WorkOrderDetailPage) },
          { path: "/nalozi/:id/uredi", ...page(workOrders, (m) => m.WorkOrderFormPage) },
          { path: "/proizvodnja", ...page(production, (m) => m.ProductionListPage) },
          { path: "/proizvodnja/novi", ...page(production, (m) => m.ProductionEntryFormPage) },
          { path: "/proizvodnja/:id", ...page(production, (m) => m.ProductionEntryFormPage) },
          {
            path: "/zalihe",
            ...page(stock, (m) => m.StockLayout),
            children: [
              { index: true, element: <Navigate to="/zalihe/artikli" replace /> },
              { path: "artikli", ...page(stock, (m) => m.ItemsPage) },
              { path: "artikli/novi", ...page(stock, (m) => m.ItemFormPage) },
              { path: "artikli/:id", ...page(stock, (m) => m.ItemFormPage) },
              { path: "lotovi", ...page(stock, (m) => m.LotsPage) },
              { path: "lotovi/prijem", ...page(stock, (m) => m.ReceiveLotPage) },
              { path: "lotovi/:id", ...page(stock, (m) => m.LotDetailPage) },
            ],
          },
          {
            path: "/kvalitet",
            ...page(quality, (m) => m.QualityLayout),
            children: [
              { index: true, element: <Navigate to="/kvalitet/kontrole" replace /> },
              { path: "kontrole", ...page(quality, (m) => m.ChecksPage) },
              { path: "kontrole/:id", ...page(quality, (m) => m.CheckDetailPage) },
              { path: "nc", ...page(quality, (m) => m.NcListPage) },
              { path: "nc/nova", ...page(quality, (m) => m.NcFormPage) },
              { path: "nc/:id", ...page(quality, (m) => m.NcFormPage) },
            ],
          },
          { path: "/dobavljaci", ...page(suppliers, (m) => m.SupplierListPage) },
          { path: "/dobavljaci/novi", ...page(suppliers, (m) => m.SupplierFormPage) },
          { path: "/dobavljaci/:id", ...page(suppliers, (m) => m.SupplierFormPage) },
          { path: "/oprema", ...page(equipment, (m) => m.EquipmentListPage) },
          { path: "/oprema/nova", ...page(equipment, (m) => m.EquipmentFormPage) },
          { path: "/oprema/:id", ...page(equipment, (m) => m.EquipmentFormPage) },
          { path: "/dokumenti", ...page(documents, (m) => m.DocumentListPage) },
          { path: "/dokumenti/novi", ...page(documents, (m) => m.DocumentFormPage) },
          { path: "/dokumenti/:id", ...page(documents, (m) => m.DocumentFormPage) },
          {
            element: <RequireRole roles={["Admin"]} />,
            children: [
              {
                path: "/podesavanja",
                ...page(settings, (m) => m.SettingsLayout),
                children: [
                  { index: true, element: <Navigate to="/podesavanja/zaposlenici" replace /> },
                  { path: "zaposlenici", ...page(settings, (m) => m.EmployeesPage) },
                  { path: "zaposlenici/novi", ...page(settings, (m) => m.EmployeeFormPage) },
                  { path: "zaposlenici/:id", ...page(settings, (m) => m.EmployeeFormPage) },
                  { path: "korisnici", ...page(settings, (m) => m.UsersPage) },
                ],
              },
            ],
          },
          { path: "*", element: <NotFoundPage /> },
        ],
      },
    ],
  },
]);
