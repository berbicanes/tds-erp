import { createBrowserRouter, Navigate } from "react-router";
import { Layout } from "./components/Layout";
import { EmptyModulePage } from "./components/EmptyModulePage";
import { LoginPage } from "./features/auth/LoginPage";
import { RequireAuth } from "./features/auth/RequireAuth";
import { RequireRole } from "./features/auth/RequireRole";
import { NotFoundPage } from "./features/NotFoundPage";
import { SettingsLayout } from "./features/settings/SettingsLayout";
import { EmployeesPage } from "./features/settings/EmployeesPage";
import { EmployeeFormPage } from "./features/settings/EmployeeFormPage";
import { UsersPage } from "./features/settings/UsersPage";
import { EquipmentListPage } from "./features/equipment/EquipmentListPage";
import { EquipmentFormPage } from "./features/equipment/EquipmentFormPage";
import { SupplierListPage } from "./features/suppliers/SupplierListPage";
import { SupplierFormPage } from "./features/suppliers/SupplierFormPage";
import { StockLayout } from "./features/stock/StockLayout";
import { ItemsPage } from "./features/stock/ItemsPage";
import { ItemFormPage } from "./features/stock/ItemFormPage";
import { LotsPage } from "./features/stock/LotsPage";
import { ReceiveLotPage } from "./features/stock/ReceiveLotPage";
import { LotDetailPage } from "./features/stock/LotDetailPage";
import { WorkOrderListPage } from "./features/workOrders/WorkOrderListPage";
import { WorkOrderFormPage } from "./features/workOrders/WorkOrderFormPage";
import { WorkOrderDetailPage } from "./features/workOrders/WorkOrderDetailPage";

export const router = createBrowserRouter([
  { path: "/prijava", element: <LoginPage /> },
  {
    element: <RequireAuth />,
    children: [
      {
        element: <Layout />,
        children: [
          { path: "/", element: <EmptyModulePage title="Pregled" /> },
          { path: "/brzi-unos", element: <EmptyModulePage title="Brzi unos" /> },
          { path: "/nalozi", element: <WorkOrderListPage /> },
          { path: "/nalozi/novi", element: <WorkOrderFormPage /> },
          { path: "/nalozi/:id", element: <WorkOrderDetailPage /> },
          { path: "/nalozi/:id/uredi", element: <WorkOrderFormPage /> },
          { path: "/proizvodnja", element: <EmptyModulePage title="Dnevna proizvodnja" /> },
          {
            path: "/zalihe",
            element: <StockLayout />,
            children: [
              { index: true, element: <Navigate to="/zalihe/artikli" replace /> },
              { path: "artikli", element: <ItemsPage /> },
              { path: "artikli/novi", element: <ItemFormPage /> },
              { path: "artikli/:id", element: <ItemFormPage /> },
              { path: "lotovi", element: <LotsPage /> },
              { path: "lotovi/prijem", element: <ReceiveLotPage /> },
              { path: "lotovi/:id", element: <LotDetailPage /> },
            ],
          },
          { path: "/kvalitet", element: <EmptyModulePage title="Kvalitet" /> },
          { path: "/dobavljaci", element: <SupplierListPage /> },
          { path: "/dobavljaci/novi", element: <SupplierFormPage /> },
          { path: "/dobavljaci/:id", element: <SupplierFormPage /> },
          { path: "/oprema", element: <EquipmentListPage /> },
          { path: "/oprema/nova", element: <EquipmentFormPage /> },
          { path: "/oprema/:id", element: <EquipmentFormPage /> },
          { path: "/dokumenti", element: <EmptyModulePage title="Dokumenti" /> },
          {
            element: <RequireRole roles={["Admin"]} />,
            children: [
              {
                path: "/podesavanja",
                element: <SettingsLayout />,
                children: [
                  { index: true, element: <Navigate to="/podesavanja/zaposlenici" replace /> },
                  { path: "zaposlenici", element: <EmployeesPage /> },
                  { path: "zaposlenici/novi", element: <EmployeeFormPage /> },
                  { path: "zaposlenici/:id", element: <EmployeeFormPage /> },
                  { path: "korisnici", element: <UsersPage /> },
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
