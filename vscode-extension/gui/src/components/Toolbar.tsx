import { useNavigate, useLocation } from "react-router-dom";
import { useAppDispatch, useAppSelector } from "../store/hooks";
import { newConversation } from "../store/sessionSlice";
import { postToExtension } from "../vscode";

export default function Toolbar() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const location = useLocation();
  const model = useAppSelector((s) => s.config.model);
  const conversations = useAppSelector((s) => s.session.conversations);
  const activeId = useAppSelector((s) => s.session.activeConversationId);
  const activeConv = conversations.find((c) => c.id === activeId);

  const onNewConversation = () => {
    dispatch(newConversation());
    navigate("/");
  };

  const onSwitchModel = () => {
    postToExtension({ type: "requestSwitchModel" });
  };

  return (
    <div
      className="flex items-center gap-1 px-2 py-1 flex-shrink-0 border-b"
      style={{ borderColor: "var(--vscode-sideBarSectionHeader-border)" }}
    >
      <button
        title="History"
        onClick={() => navigate(location.pathname === "/history" ? "/" : "/history")}
        className="p-1 rounded opacity-70 hover:opacity-100 hover:bg-[var(--vscode-toolbar-hoverBackground)] text-sm"
      >
        ☰
      </button>

      <span className="flex-1 text-xs font-semibold truncate opacity-80">
        {activeConv?.title ?? "AI Coding Assistant"}
      </span>

      <button
        title="Settings"
        onClick={() => navigate(location.pathname === "/settings" ? "/" : "/settings")}
        className="p-1 rounded opacity-70 hover:opacity-100 hover:bg-[var(--vscode-toolbar-hoverBackground)] text-sm"
      >
        ⚙
      </button>

      <button
        title={`Model: ${model} — click to switch`}
        onClick={onSwitchModel}
        className="px-1.5 py-0.5 rounded text-xs opacity-70 hover:opacity-100 hover:bg-[var(--vscode-toolbar-hoverBackground)] font-mono"
      >
        {model.replace("assistant-", "")}
      </button>

      <button
        title="New conversation (Ctrl+Shift+N)"
        onClick={onNewConversation}
        className="p-1 rounded opacity-70 hover:opacity-100 hover:bg-[var(--vscode-toolbar-hoverBackground)] text-base font-bold"
      >
        ＋
      </button>
    </div>
  );
}
