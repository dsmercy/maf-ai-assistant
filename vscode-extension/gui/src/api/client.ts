export interface ChatMessage {
  role: "user" | "assistant" | "system" | "tool";
  content: string | null;
  tool_calls?: ToolCall[];
  tool_call_id?: string;
}

export interface ToolCall {
  id: string;
  type: "function";
  function: { name: string; arguments: string };
}

export interface StreamChunk {
  type: "token" | "tool_call" | "done" | "error";
  token?: string;
  toolCall?: ToolCall;
  error?: string;
}

function getBaseUrl(): string {
  try {
    const stored = localStorage.getItem("aiAssistant.apiBaseUrl");
    if (stored) { return JSON.parse(stored); }
  } catch {}
  return "http://localhost:5016";
}

function getModel(): string {
  try {
    const stored = localStorage.getItem("aiAssistant.model");
    if (stored) { return JSON.parse(stored); }
  } catch {}
  return "assistant-30b";
}

// Tool schemas sent to the LLM so it can create/edit files
export const FILE_TOOLS = [
  {
    type: "function",
    function: {
      name: "create_new_file",
      description: "Create a new file with the given content. Use for new files that don't exist yet.",
      parameters: {
        type: "object",
        required: ["filepath", "contents"],
        properties: {
          filepath: {
            type: "string",
            description: "Relative path from workspace root where the file should be created (e.g. src/App.tsx)",
          },
          contents: {
            type: "string",
            description: "The complete file contents to write",
          },
        },
      },
    },
  },
  {
    type: "function",
    function: {
      name: "edit_existing_file",
      description: "Edit an existing file by providing the complete new file contents.",
      parameters: {
        type: "object",
        required: ["filepath", "contents"],
        properties: {
          filepath: {
            type: "string",
            description: "Relative path from workspace root of the file to edit",
          },
          contents: {
            type: "string",
            description: "The complete new file contents",
          },
        },
      },
    },
  },
];

export async function* streamChat(
  messages: ChatMessage[],
  signal?: AbortSignal,
  includeTools = false
): AsyncGenerator<StreamChunk> {
  const baseUrl = getBaseUrl();
  const model = getModel();

  const body = JSON.stringify({
    model,
    messages,
    stream: true,
    ...(includeTools ? { tools: FILE_TOOLS, tool_choice: "auto" } : {}),
  });

  let response: Response;
  try {
    response = await fetch(`${baseUrl}/v1/chat/completions`, {
      method: "POST",
      headers: {
        "Content-Type": "application/json",
        Authorization: "Bearer none",
      },
      body,
      signal,
    });
  } catch (err: unknown) {
    yield { type: "error", error: `Cannot reach API at ${baseUrl}. Is it running?` };
    return;
  }

  if (!response.ok) {
    yield { type: "error", error: `API error ${response.status}: ${await response.text()}` };
    return;
  }

  const reader = response.body?.getReader();
  if (!reader) { yield { type: "error", error: "No response body" }; return; }

  const decoder = new TextDecoder();
  let buffer = "";
  // Accumulate partial tool_call arguments across chunks
  const pendingToolCalls: Record<number, ToolCall> = {};

  while (true) {
    const { done, value } = await reader.read();
    if (done) { break; }

    buffer += decoder.decode(value, { stream: true });
    const lines = buffer.split("\n");
    buffer = lines.pop() ?? "";

    for (const line of lines) {
      const trimmed = line.trim();
      if (!trimmed.startsWith("data:")) { continue; }
      const data = trimmed.slice(5).trim();
      if (data === "[DONE]") { yield { type: "done" }; return; }

      try {
        const parsed = JSON.parse(data);
        const delta = parsed?.choices?.[0]?.delta;
        if (!delta) { continue; }

        // Regular text token
        if (delta.content) {
          yield { type: "token", token: delta.content };
        }

        // Tool call chunks (streamed incrementally)
        if (delta.tool_calls) {
          for (const tc of delta.tool_calls) {
            const idx: number = tc.index ?? 0;
            if (!pendingToolCalls[idx]) {
              pendingToolCalls[idx] = {
                id: tc.id ?? `call_${idx}`,
                type: "function",
                function: { name: tc.function?.name ?? "", arguments: "" },
              };
            }
            if (tc.function?.name) {
              pendingToolCalls[idx].function.name = tc.function.name;
            }
            if (tc.function?.arguments) {
              pendingToolCalls[idx].function.arguments += tc.function.arguments;
            }
          }
        }

        // finish_reason signals tool_calls are complete
        const finishReason = parsed?.choices?.[0]?.finish_reason;
        if (finishReason === "tool_calls") {
          for (const tc of Object.values(pendingToolCalls)) {
            yield { type: "tool_call", toolCall: tc };
          }
          yield { type: "done" };
          return;
        }
      } catch {
        // partial JSON — wait for next chunk
      }
    }
  }

  yield { type: "done" };
}

export async function fetchModels(): Promise<string[]> {
  try {
    const res = await fetch(`${getBaseUrl()}/v1/models`, {
      headers: { Authorization: "Bearer none" },
    });
    const json = await res.json();
    return (json.data ?? []).map((m: { id: string }) => m.id);
  } catch {
    return ["assistant-30b", "assistant-14b", "ai-assistant"];
  }
}
