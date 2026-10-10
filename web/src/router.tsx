import { createBrowserRouter } from "react-router";
import { Layout } from "./components/Layout";
import { EmptyModulePage } from "./components/EmptyModulePage";
import { LoginPage } from "./features/auth/LoginPage";
import { RequireAuth } from "./features/auth/RequireAuth";
import { RequireRole } from "./features/auth/RequireRole";
import { NotFoundPage } from "./features/NotFoundPage";

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
            children: [{ path: "/podesavanja", element: <EmptyModulePage title="Podešavanja" /> }],
          },
          { path: "*", element: <NotFoundPage /> },
        ],
      },
    ],
  },
]);
