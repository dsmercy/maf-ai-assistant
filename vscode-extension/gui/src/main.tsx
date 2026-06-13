import React from "react";
import ReactDOM from "react-dom/client";
import { Provider } from "react-redux";
import { PersistGate } from "redux-persist/integration/react";
import App from "./App";
import { store, persistor } from "./store";
import "./index.css";

function showError(msg: string) {
  const root = document.getElementById("root");
  if (root) {
    root.innerHTML = `<pre style="color:red;padding:8px;font-size:11px;white-space:pre-wrap;background:#1e1e1e">${msg}</pre>`;
  }
}

window.addEventListener("error", (e) => showError(`Window error:\n${e.message}\n${e.filename}:${e.lineno}`));
window.addEventListener("unhandledrejection", (e) => showError(`Unhandled rejection:\n${String(e.reason)}`));

try {
  const root = document.getElementById("root");
  if (!root) { throw new Error("No #root element found in DOM"); }

  ReactDOM.createRoot(root).render(
    <React.StrictMode>
      <Provider store={store}>
        <PersistGate loading={<div style={{padding:8,color:"#ccc",fontSize:12}}>Restoring session...</div>} persistor={persistor}>
          <App />
        </PersistGate>
      </Provider>
    </React.StrictMode>
  );
} catch (e) {
  showError(`Startup error:\n${String(e)}\n${e instanceof Error ? e.stack ?? "" : ""}`);
}
