import { useEffect, useRef, useCallback, useState } from "react";
import { useAppDispatch, useAppSelector } from "../store/hooks";
import {
  addUserMessage,
  appendStreamToken,
  streamDone,
  addToolCallsReceived,
  setError,
  newConversation,
} from "../store/sessionSlice";
import { setConfig } from "../store/configSlice";
import { streamChat } from "../api/client";
import MarkdownRenderer from "../components/MarkdownRenderer";
import FileToolCallCard from "../components/FileToolCallCard";
import ChatInput from "../components/ChatInput";
import { postToExtension } from "../vscode";

export default function ChatPage() {
  const dispatch = useAppDispatch();
  const conversations = useAppSelector((s) => s.session.conversations);
  const activeId = useAppSelector((s) => s.session.activeConversationId);
  const isStreaming = useAppSelector((s) => s.session.isStreaming);
  const streamingContent = useAppSelector((s) => s.session.streamingMessageContent);
  const error = useAppSelector((s) => s.session.error);
  const includeFileTools = useAppSelector((s) => s.config.includeFileTools);

  const activeConv = conversations.find((c) => c.id === activeId);
  const messagesEndRef = useRef<HTMLDivElement>(null);
  const abortRef = useRef<AbortController | null>(null);

  // Ensure there's always an active conversation
  useEffect(() => {
    if (!activeId || !activeConv) {
      dispatch(newConversation());
    }
  }, []);

  // Request config from extension host on mount
  useEffect(() => {
    postToExtension({ type: "getConfig" });
  }, []);

  // Auto-scroll on new content
  useEffect(() => {
    messagesEndRef.current?.scrollIntoView({ behavior: "smooth" });
  }, [activeConv?.messages.length, streamingContent]);

  const handleSend = useCallback(
    async (text: string, withTools: boolean) => {
      if (!activeConv) { return; }

      dispatch(addUserMessage({ content: text }));

      const messages = [
        ...activeConv.messages,
        { role: "user" as const, content: text },
      ];

      abortRef.current = new AbortController();
      const collected: import("../api/client").ToolCall[] = [];

      try {
        for await (const chunk of streamChat(messages, abortRef.current.signal, withTools)) {
          if (chunk.type === "token") {
            dispatch(appendStreamToken(chunk.token!));
          } else if (chunk.type === "tool_call") {
            collected.push(chunk.toolCall!);
          } else if (chunk.type === "done") {
            if (collected.length > 0) {
              dispatch(addToolCallsReceived(collected));
              // Read original contents for diff view
              for (const tc of collected) {
                if (tc.function.name === "edit_existing_file") {
                  let args: { filepath?: string } = {};
                  try { args = JSON.parse(tc.function.arguments); } catch {}
                  if (args.filepath) {
                    postToExtension({ type: "readFile", filePath: args.filepath });
                  }
                }
              }
            } else {
              dispatch(streamDone());
            }
            break;
          } else if (chunk.type === "error") {
            dispatch(setError(chunk.error!));
            break;
          }
        }
      } catch (err: unknown) {
        if (err instanceof Error && err.name !== "AbortError") {
          dispatch(setError(err.message));
        } else {
          dispatch(streamDone());
        }
      }
    },
    [activeConv, dispatch]
  );

  const handleCancel = () => {
    abortRef.current?.abort();
    dispatch(streamDone());
  };

  const toggleTools = () => {
    dispatch(setConfig({ includeFileTools: !includeFileTools }));
  };

  // Pending file tool calls for this conversation
  const pendingFileCalls = activeConv?.fileToolCalls ?? [];

  // Apply all pending files at once
  const applyAll = () => {
    const pending = pendingFileCalls.filter((f) => f.status === "pending");
    for (const ftc of pending) {
      postToExtension({ type: "writeFile", filePath: ftc.filePath, content: ftc.contents });
      dispatch({ type: "session/applyFileToolCall", payload: ftc.id });
    }
  };

  if (!activeConv) { return null; }

  return (
    <div className="flex flex-col h-full overflow-hidden">
      {/* Messages */}
      <div className="flex-1 overflow-y-auto p-3 flex flex-col gap-3">
        {activeConv.messages.length === 0 && (
          <div className="flex flex-col items-center justify-center h-full opacity-40 text-sm gap-2">
            <span className="text-3xl">🤖</span>
            <span>Ask anything, or request a new project.</span>
            <span className="text-xs">Enable File Tools to let the AI write files directly.</span>
          </div>
        )}

        {activeConv.messages.map((msg, i) => {
          // Skip null-content assistant messages (tool_calls turns)
          if (msg.role === "assistant" && msg.content === null) { return null; }
          if (msg.role === "tool") { return null; }
          if (msg.role === "system") { return null; }

          return (
            <div
              key={i}
              className={`flex ${msg.role === "user" ? "justify-end" : "justify-start w-full"}`}
            >
              {msg.role === "user" ? (
                <div
                  className="max-w-[90%] px-3 py-2 rounded-lg text-sm whitespace-pre-wrap"
                  style={{
                    background: "var(--vscode-input-background)",
                    border: "1px solid var(--vscode-input-border)",
                  }}
                >
                  {msg.content}
                </div>
              ) : (
                <div
                  className="w-full px-3 py-2 rounded-lg border-l-2"
                  style={{
                    background: "var(--vscode-editor-inactiveSelectionBackground)",
                    borderLeftColor: "var(--vscode-activityBarBadge-background)",
                  }}
                >
                  <MarkdownRenderer content={msg.content ?? ""} />
                  {/* File tool calls attached to this message */}
                  {activeConv.fileToolCalls
                    .filter((_, fi) => {
                      // Associate tool calls following this assistant message
                      const nextMsgs = activeConv.messages.slice(i + 1);
                      return nextMsgs.findIndex((m) => m.tool_calls) === 0 && fi === 0;
                    })
                    .map(() => null)}
                </div>
              )}
            </div>
          );
        })}

        {/* File tool call cards — shown after all messages */}
        {pendingFileCalls.length > 0 && (
          <div className="w-full">
            {pendingFileCalls.filter((f) => f.status === "pending").length > 1 && (
              <div className="flex justify-end mb-1">
                <button
                  onClick={applyAll}
                  className="px-3 py-1 rounded text-[11px] font-semibold"
                  style={{
                    background: "var(--vscode-button-background)",
                    color: "var(--vscode-button-foreground)",
                  }}
                >
                  ⚡ Apply all {pendingFileCalls.filter((f) => f.status === "pending").length} files
                </button>
              </div>
            )}
            {pendingFileCalls.map((ftc) => (
              <FileToolCallCard key={ftc.id} toolCall={ftc} />
            ))}
          </div>
        )}

        {/* Streaming assistant message */}
        {isStreaming && (
          <div
            className="w-full px-3 py-2 rounded-lg border-l-2"
            style={{
              background: "var(--vscode-editor-inactiveSelectionBackground)",
              borderLeftColor: "var(--vscode-activityBarBadge-background)",
            }}
          >
            <MarkdownRenderer content={streamingContent} streaming={true} />
          </div>
        )}

        {/* Error message */}
        {error && (
          <div
            className="px-3 py-2 rounded text-sm"
            style={{
              background: "var(--vscode-inputValidation-errorBackground)",
              border: "1px solid var(--vscode-inputValidation-errorBorder)",
            }}
          >
            {error}
          </div>
        )}

        <div ref={messagesEndRef} />
      </div>

      {/* Input */}
      <ChatInput
        onSend={handleSend}
        onCancel={handleCancel}
        streaming={isStreaming}
        includeTools={includeFileTools}
        onToggleTools={toggleTools}
      />
    </div>
  );
}
