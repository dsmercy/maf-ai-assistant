import { Outlet } from "react-router-dom";
import Toolbar from "./Toolbar";

export default function Layout() {
  return (
    <div className="flex flex-col h-screen overflow-hidden">
      <Toolbar />
      <div className="flex-1 overflow-hidden">
        <Outlet />
      </div>
    </div>
  );
}
