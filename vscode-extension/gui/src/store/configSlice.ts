import { createSlice, PayloadAction } from "@reduxjs/toolkit";

interface ConfigState {
  apiBaseUrl: string;
  model: string;
  streamResponses: boolean;
  autoAcceptEdits: boolean;
  includeFileTools: boolean;
}

const initialState: ConfigState = {
  apiBaseUrl: "http://localhost:5016",
  model: "assistant-30b",
  streamResponses: true,
  autoAcceptEdits: false,
  includeFileTools: true,
};

const configSlice = createSlice({
  name: "config",
  initialState,
  reducers: {
    setConfig(state, action: PayloadAction<Partial<ConfigState>>) {
      return { ...state, ...action.payload };
    },
    setModel(state, action: PayloadAction<string>) {
      state.model = action.payload;
      // also update localStorage so the API client picks it up immediately
      localStorage.setItem("aiAssistant.model", JSON.stringify(action.payload));
    },
  },
});

export const { setConfig, setModel } = configSlice.actions;
export const configReducer = configSlice.reducer;
