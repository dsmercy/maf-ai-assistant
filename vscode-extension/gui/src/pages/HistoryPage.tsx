import { useNavigate } from "react-router-dom";
import { useAppDispatch, useAppSelector } from "../store/hooks";
import { loadConversation, deleteConversation } from "../store/sessionSlice";

export default function HistoryPage() {
  const dispatch = useAppDispatch();
  const navigate = useNavigate();
  const conversations = useAppSelector((s) => s.session.conversations);
  const activeId = useAppSelector((s) => s.session.activeConversationId);

  const formatDate = (ts: number) => {
    const d = new Date(ts);
    const now = new Date();
    if (d.toDateString() === now.toDateString()) {
      return d.toLocaleTimeString([], { hour: "2-digit", minute: "2-digit" });
    }
    return d.toLocaleDateString([], { month: "short", day: "numeric" });
  };

  const load = (id: string) => {
    dispatch(loadConversation(id));
    navigate("/");
  };

  const del = (e: React.MouseEvent, id: string) => {
    e.stopPropagation();
    dispatch(deleteConversation(id));
  };

  return (
    <div className="flex flex-col h-full overflow-hidden">
      <div className="px-3 py-2 text-xs font-semibold opacity-60 border-b flex-shrink-0"
        style={{ borderColor: "var(--vscode-sideBarSectionHeader-border)" }}>
        Conversations ({conversations.length})
      </div>
      <div className="flex-1 overflow-y-auto">
        {conversations.length === 0 && (
          <div className="p-4 text-xs opacity-40 text-center">No conversations yet</div>
        )}
        {conversations.map((conv) => (
          <div
            key={conv.id}
            onClick={() => load(conv.id)}
            className="flex items-center gap-2 px-3 py-2 cursor-pointer group border-b"
            style={{
              borderColor: "var(--vscode-sideBarSectionHeader-border)",
              background: conv.id === activeId
                ? "var(--vscode-list-activeSelectionBackground)"
                : undefined,
            }}
          >
            <div className="flex-1 min-w-0">
              <div className="text-xs font-medium truncate">{conv.title}</div>
              <div className="text-[10px] opacity-50 mt-0.5">
                {conv.messages.filter((m) => m.role === "user").length} messages · {formatDate(conv.updatedAt)}
              </div>
            </div>
            <button
              onClick={(e) => del(e, conv.id)}
              className="opacity-0 group-hover:opacity-60 hover:!opacity-100 p-0.5 rounded text-xs"
              title="Delete conversation"
            >
              ✕
            </button>
          </div>
        ))}
      </div>
    </div>
  );
}
