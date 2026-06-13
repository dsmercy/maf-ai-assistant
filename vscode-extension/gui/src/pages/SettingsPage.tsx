import { useNavigate } from "react-router-dom";
import { useAppDispatch, useAppSelector } from "../store/hooks";
import { setConfig } from "../store/configSlice";

export default function SettingsPage() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const config = useAppSelector((s) => s.config);

  const update = (key: string, value: unknown) =>
    dispatch(setConfig({ [key]: value }));

  return (
    <div className="flex flex-col h-full overflow-y-auto p-3 gap-4 text-sm">
      <div className="flex items-center justify-between">
        <span className="font-semibold text-xs opacity-60">SETTINGS</span>
        <button onClick={() => navigate("/")} className="text-xs opacity-50 hover:opacity-100">✕ Close</button>
      </div>

      <div className="flex flex-col gap-3">
        <Field label="API Base URL">
          <input
            className="w-full rounded px-2 py-1 text-xs"
            style={{
              background: "var(--vscode-input-background)",
              color: "var(--vscode-input-foreground)",
              border: "1px solid var(--vscode-input-border)",
            }}
            value={config.apiBaseUrl}
            onChange={(e) => update("apiBaseUrl", e.target.value)}
          />
        </Field>

        <Field label="Model">
          <select
            className="w-full rounded px-2 py-1 text-xs"
            style={{
              background: "var(--vscode-input-background)",
              color: "var(--vscode-input-foreground)",
              border: "1px solid var(--vscode-input-border)",
            }}
            value={config.model}
            onChange={(e) => update("model", e.target.value)}
          >
            <option value="assistant-30b">assistant-30b</option>
            <option value="assistant-14b">assistant-14b</option>
            <option value="ai-assistant">ai-assistant</option>
          </select>
        </Field>

        <Field label="Stream Responses">
          <Toggle
            checked={config.streamResponses}
            onChange={(v) => update("streamResponses", v)}
          />
        </Field>

        <Field label="Auto-accept File Edits">
          <Toggle
            checked={config.autoAcceptEdits}
            onChange={(v) => update("autoAcceptEdits", v)}
          />
        </Field>

        <Field label="Include File Tools">
          <Toggle
            checked={config.includeFileTools}
            onChange={(v) => update("includeFileTools", v)}
          />
          <span className="text-[10px] opacity-50 mt-1">
            Enables AI to create/edit files directly when generating projects
          </span>
        </Field>
      </div>
    </div>
  );
}

function Field({ label, children }: { label: string; children: React.ReactNode }) {
  return (
    <div className="flex flex-col gap-1">
      <label className="text-[11px] font-medium opacity-70">{label}</label>
      {children}
    </div>
  );
}

function Toggle({ checked, onChange }: { checked: boolean; onChange: (v: boolean) => void }) {
  return (
    <button
      onClick={() => onChange(!checked)}
      className="w-8 h-4 rounded-full relative flex-shrink-0"
      style={{
        background: checked
          ? "var(--vscode-button-background)"
          : "var(--vscode-button-secondaryBackground)",
      }}
    >
      <span
        className="absolute top-0.5 w-3 h-3 rounded-full transition-all"
        style={{
          left: checked ? "calc(100% - 14px)" : 2,
          background: "white",
        }}
      />
    </button>
  );
}
