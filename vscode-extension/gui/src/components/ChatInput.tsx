import { useRef, useState, KeyboardEvent } from "react";
import TextareaAutosize from "react-textarea-autosize";

interface Props {
  onSend: (text: string, includeTools: boolean) => void;
  onCancel: () => void;
  streaming: boolean;
  includeTools: boolean;
  onToggleTools: () => void;
}

export default function ChatInput({ onSend, onCancel, streaming, includeTools, onToggleTools }: Props) {
  const [text, setText] = useState("");
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const send = () => {
    const trimmed = text.trim();
    if (!trimmed || streaming) { return; }
    onSend(trimmed, includeTools);
    setText("");
  };

  const onKeyDown = (e: KeyboardEvent<HTMLTextAreaElement>) => {
    if (e.key === "Enter" && !e.shiftKey) {
      e.preventDefault();
      send();
    }
  };

  return (
    <div
      className="flex-shrink-0 border-t"
      style={{ borderColor: "var(--vscode-sideBarSectionHeader-border)" }}
    >
      {/* Tool toggle bar */}
      <div className="flex items-center gap-2 px-2 py-1 border-b" style={{ borderColor: "var(--vscode-sideBarSectionHeader-border)" }}>
        <button
          onClick={onToggleTools}
          title={includeTools ? "File tools enabled — AI can create/edit files" : "File tools disabled — chat only"}
          className="flex items-center gap-1 text-[11px] px-2 py-0.5 rounded"
          style={{
            background: includeTools
              ? "var(--vscode-button-background)"
              : "var(--vscode-button-secondaryBackground)",
            color: includeTools
              ? "var(--vscode-button-foreground)"
              : "var(--vscode-button-secondaryForeground)",
          }}
        >
          {includeTools ? "🗂 File tools ON" : "💬 Chat only"}
        </button>
        <span className="text-[10px] opacity-50">
          {includeTools ? "AI will create/edit files directly" : "AI responds in chat only"}
        </span>
      </div>

      {/* Input row */}
      <div className="flex gap-2 p-2 items-end">
        <TextareaAutosize
          ref={textareaRef}
          value={text}
          onChange={(e) => setText(e.target.value)}
          onKeyDown={onKeyDown}
          placeholder="Ask anything… (Enter to send, Shift+Enter for new line)"
          minRows={1}
          maxRows={8}
          disabled={streaming}
          className="flex-1 resize-none rounded px-2 py-1.5 text-[13px] outline-none"
          style={{
            background: "var(--vscode-input-background)",
            color: "var(--vscode-input-foreground)",
            border: "1px solid var(--vscode-input-border)",
            fontFamily: "var(--vscode-font-family)",
          }}
        />
        <button
          onClick={streaming ? onCancel : send}
          className="px-3 py-1.5 rounded text-[13px] font-medium flex-shrink-0"
          style={{
            background: streaming
              ? "var(--vscode-button-secondaryBackground)"
              : "var(--vscode-button-background)",
            color: streaming
              ? "var(--vscode-button-secondaryForeground)"
              : "var(--vscode-button-foreground)",
            minWidth: 56,
          }}
        >
          {streaming ? "◼" : "Send"}
        </button>
      </div>
    </div>
  );
}
