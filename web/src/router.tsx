import { createBrowserRouter } from "react-router";
import { LoginPage } from "./features/auth/LoginPage";
import { RequireAuth } from "./features/auth/RequireAuth";
import { HomePlaceholder } from "./features/HomePlaceholder";

export const router = createBrowserRouter([
  { path: "/prijava", element: <LoginPage /> },
  {
    element: <RequireAuth />,
    children: [{ path: "/", element: <HomePlaceholder /> }],
  },
]);
