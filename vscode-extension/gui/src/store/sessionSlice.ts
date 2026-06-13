import { createSlice, PayloadAction } from "@reduxjs/toolkit";
import { ChatMessage, ToolCall } from "../api/client";
import { v4 as uuidv4 } from "uuid";

export interface FileToolCall {
  id: string;
  toolCallId: string;
  toolName: "create_new_file" | "edit_existing_file";
  filePath: string;
  contents: string;
  originalContents: string; // populated when file is read back
  status: "pending" | "applying" | "applied" | "rejected";
}

export interface Conversation {
  id: string;
  title: string;
  createdAt: number;
  updatedAt: number;
  messages: ChatMessage[];
  fileToolCalls: FileToolCall[];
}

interface SessionState {
  conversations: Conversation[];
  activeConversationId: string | null;
  streamingMessageContent: string;
  isStreaming: boolean;
  pendingToolCalls: FileToolCall[];
  error: string | null;
}

const MAX_CONVERSATIONS = 50;

const initialState: SessionState = {
  conversations: [],
  activeConversationId: null,
  streamingMessageContent: "",
  isStreaming: false,
  pendingToolCalls: [],
  error: null,
};

const sessionSlice = createSlice({
  name: "session",
  initialState,
  reducers: {
    newConversation(state) {
      const conv: Conversation = {
        id: uuidv4(),
        title: "New conversation",
        createdAt: Date.now(),
        updatedAt: Date.now(),
        messages: [],
        fileToolCalls: [],
      };
      state.conversations = [conv, ...state.conversations].slice(0, MAX_CONVERSATIONS);
      state.activeConversationId = conv.id;
      state.streamingMessageContent = "";
      state.isStreaming = false;
      state.pendingToolCalls = [];
      state.error = null;
    },

    loadConversation(state, action: PayloadAction<string>) {
      state.activeConversationId = action.payload;
      state.streamingMessageContent = "";
      state.isStreaming = false;
      state.pendingToolCalls = [];
      state.error = null;
    },

    deleteConversation(state, action: PayloadAction<string>) {
      state.conversations = state.conversations.filter((c) => c.id !== action.payload);
      if (state.activeConversationId === action.payload) {
        state.activeConversationId = state.conversations[0]?.id ?? null;
      }
    },

    addUserMessage(state, action: PayloadAction<{ content: string }>) {
      const conv = state.conversations.find((c) => c.id === state.activeConversationId);
      if (!conv) { return; }
      conv.messages.push({ role: "user", content: action.payload.content });
      // Auto-title from first user message
      if (conv.title === "New conversation") {
        conv.title = action.payload.content.slice(0, 60).replace(/\n/g, " ") +
          (action.payload.content.length > 60 ? "…" : "");
      }
      conv.updatedAt = Date.now();
      state.isStreaming = true;
      state.streamingMessageContent = "";
      state.pendingToolCalls = [];
      state.error = null;
    },

    appendStreamToken(state, action: PayloadAction<string>) {
      state.streamingMessageContent += action.payload;
    },

    streamDone(state) {
      const conv = state.conversations.find((c) => c.id === state.activeConversationId);
      if (conv && state.streamingMessageContent) {
        conv.messages.push({ role: "assistant", content: state.streamingMessageContent });
        conv.updatedAt = Date.now();
      }
      state.isStreaming = false;
      state.streamingMessageContent = "";
    },

    addToolCallsReceived(state, action: PayloadAction<ToolCall[]>) {
      const conv = state.conversations.find((c) => c.id === state.activeConversationId);
      if (!conv) { return; }

      // Save assistant message with tool_calls (content must be null per OpenAI spec)
      conv.messages.push({
        role: "assistant",
        content: null,
        tool_calls: action.payload,
      });

      const fileToolCalls: FileToolCall[] = action.payload
        .filter((tc) =>
          tc.function.name === "create_new_file" ||
          tc.function.name === "edit_existing_file"
        )
        .map((tc) => {
          let args: { filepath?: string; contents?: string } = {};
          try { args = JSON.parse(tc.function.arguments); } catch {}
          return {
            id: uuidv4(),
            toolCallId: tc.id,
            toolName: tc.function.name as "create_new_file" | "edit_existing_file",
            filePath: args.filepath ?? "",
            contents: args.contents ?? "",
            originalContents: "",
            status: "pending",
          };
        });

      conv.fileToolCalls.push(...fileToolCalls);
      state.pendingToolCalls = fileToolCalls;
      state.isStreaming = false;
      state.streamingMessageContent = "";
      conv.updatedAt = Date.now();
    },

    setOriginalContents(
      state,
      action: PayloadAction<{ toolCallId: string; originalContents: string }>
    ) {
      const conv = state.conversations.find((c) => c.id === state.activeConversationId);
      if (!conv) { return; }
      const ftc = conv.fileToolCalls.find((f) => f.toolCallId === action.payload.toolCallId);
      if (ftc) { ftc.originalContents = action.payload.originalContents; }
    },

    applyFileToolCall(state, action: PayloadAction<string>) {
      const conv = state.conversations.find((c) => c.id === state.activeConversationId);
      if (!conv) { return; }
      const ftc = conv.fileToolCalls.find((f) => f.id === action.payload);
      if (ftc) { ftc.status = "applied"; }

      // If all applied, add tool result messages and clear pending
      const pending = conv.fileToolCalls.filter((f) => f.status === "pending");
      if (pending.length === 0) {
        state.pendingToolCalls = [];
      }
    },

    rejectFileToolCall(state, action: PayloadAction<string>) {
      const conv = state.conversations.find((c) => c.id === state.activeConversationId);
      if (!conv) { return; }
      const ftc = conv.fileToolCalls.find((f) => f.id === action.payload);
      if (ftc) { ftc.status = "rejected"; }
    },

    setError(state, action: PayloadAction<string>) {
      state.isStreaming = false;
      state.streamingMessageContent = "";
      state.error = action.payload;
    },

    clearError(state) {
      state.error = null;
    },
  },
});

export const {
  newConversation,
  loadConversation,
  deleteConversation,
  addUserMessage,
  appendStreamToken,
  streamDone,
  addToolCallsReceived,
  setOriginalContents,
  applyFileToolCall,
  rejectFileToolCall,
  setError,
  clearError,
} = sessionSlice.actions;

// re-export setModel from configSlice for convenience
export { setModel } from "./configSlice";

export const sessionReducer = sessionSlice.reducer;
