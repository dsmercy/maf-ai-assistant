import { useState } from "react";
import { useAppDispatch } from "../store/hooks";
import { applyFileToolCall, rejectFileToolCall, FileToolCall } from "../store/sessionSlice";
import { postToExtension } from "../vscode";
import MarkdownRenderer from "./MarkdownRenderer";

interface Props {
  toolCall: FileToolCall;
}

export default function FileToolCallCard({ toolCall }: Props) {
  const dispatch = useAppDispatch();
  const [expanded, setExpanded] = useState(false);
  const isNew = toolCall.toolName === "create_new_file";
  const ext = toolCall.filePath.split(".").pop() ?? "plaintext";

  const apply = () => {
    postToExtension({
      type: "writeFile",
      filePath: toolCall.filePath,
      content: toolCall.contents,
    });
    dispatch(applyFileToolCall(toolCall.id));
  };

  const reject = () => {
    dispatch(rejectFileToolCall(toolCall.id));
  };

  const viewDiff = () => {
    postToExtension({
      type: "showDiff",
      filePath: toolCall.filePath,
      originalContent: toolCall.originalContents,
      newContent: toolCall.contents,
    });
  };

  const openFile = () => {
    postToExtension({ type: "openFile", filePath: toolCall.filePath });
  };

  return (
    <div
      className="rounded border my-1.5 overflow-hidden text-xs"
      style={{
        borderColor: "var(--vscode-input-border)",
        background: "var(--vscode-editor-background)",
      }}
    >
      {/* Header */}
      <div
        className="flex items-center gap-2 px-2.5 py-1.5 border-b"
        style={{
          borderColor: "var(--vscode-input-border)",
          background: "var(--vscode-sideBar-background)",
        }}
      >
        <span className="text-sm">{isNew ? "📄" : "✏️"}</span>
        <span
          className="flex-1 font-mono truncate cursor-pointer hover:underline"
          style={{ fontSize: 11, color: "var(--vscode-textLink-foreground)" }}
          onClick={openFile}
          title={toolCall.filePath}
        >
          {toolCall.filePath}
        </span>
        <span
          className="px-1.5 py-0.5 rounded-full font-semibold text-[10px]"
          style={{
            background: isNew ? "#1a4a1a" : "#1a2f4a",
            color: isNew ? "#4ec94e" : "#4d9de0",
          }}
        >
          {isNew ? "NEW" : "EDIT"}
        </span>
        <button
          className="opacity-50 hover:opacity-100 px-1"
          onClick={() => setExpanded((v) => !v)}
          title={expanded ? "Collapse preview" : "Expand preview"}
        >
          {expanded ? "▲" : "▼"}
        </button>
      </div>

      {/* Preview */}
      {expanded && (
        <div
          className="overflow-auto"
          style={{
            maxHeight: 240,
            background: "var(--vscode-textCodeBlock-background)",
          }}
        >
          <MarkdownRenderer content={"```" + ext + "\n" + toolCall.contents + "\n```"} />
        </div>
      )}

      {/* Actions */}
      {toolCall.status === "pending" && (
        <div
          className="flex items-center gap-2 px-2.5 py-1.5"
          style={{ borderTop: expanded ? "1px solid var(--vscode-input-border)" : undefined }}
        >
          <button
            onClick={apply}
            className="px-3 py-0.5 rounded text-[11px] font-medium"
            style={{
              background: "var(--vscode-button-background)",
              color: "var(--vscode-button-foreground)",
            }}
          >
            Apply
          </button>
          {!isNew && (
            <button
              onClick={viewDiff}
              className="px-3 py-0.5 rounded text-[11px] font-medium"
              style={{
                background: "var(--vscode-button-secondaryBackground)",
                color: "var(--vscode-button-secondaryForeground)",
              }}
            >
              View Diff
            </button>
          )}
          <button
            onClick={reject}
            className="px-3 py-0.5 rounded text-[11px] font-medium opacity-60 hover:opacity-100"
            style={{
              background: "var(--vscode-button-secondaryBackground)",
              color: "var(--vscode-button-secondaryForeground)",
            }}
          >
            Reject
          </button>
        </div>
      )}

      {toolCall.status === "applied" && (
        <div className="px-2.5 py-1 text-[11px] opacity-60" style={{ color: "#4ec94e" }}>
          ✓ Applied
        </div>
      )}

      {toolCall.status === "rejected" && (
        <div className="px-2.5 py-1 text-[11px] opacity-60">✗ Rejected</div>
      )}
    </div>
  );
}
