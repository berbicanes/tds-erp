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
          { path: "/nalozi", element: <EmptyModulePage title="Nalozi" /> },
          { path: "/proizvodnja", element: <EmptyModulePage title="Dnevna proizvodnja" /> },
          { path: "/zalihe", element: <EmptyModulePage title="Zalihe" /> },
          { path: "/kvalitet", element: <EmptyModulePage title="Kvalitet" /> },
          { path: "/dobavljaci", element: <EmptyModulePage title="Dobavljači" /> },
          { path: "/oprema", element: <EmptyModulePage title="Oprema" /> },
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
