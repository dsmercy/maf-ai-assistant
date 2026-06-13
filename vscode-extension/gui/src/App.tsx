import { createMemoryRouter, RouterProvider } from "react-router-dom";
import ChatPage from "./pages/ChatPage";
import HistoryPage from "./pages/HistoryPage";
import SettingsPage from "./pages/SettingsPage";
import Layout from "./components/Layout";
import { useVscodeMessages } from "./hooks/useVscodeMessages";

const router = createMemoryRouter([
  {
    path: "/",
    element: <Layout />,
    children: [
      { index: true,        element: <ChatPage /> },
      { path: "history",    element: <HistoryPage /> },
      { path: "settings",   element: <SettingsPage /> },
    ],
  },
]);

export default function App() {
  // Listen for messages from the extension host
  useVscodeMessages();
  return <RouterProvider router={router} />;
}
