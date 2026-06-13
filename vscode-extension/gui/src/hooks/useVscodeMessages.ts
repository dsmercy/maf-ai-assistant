import { useEffect } from "react";
import { useAppDispatch } from "../store/hooks";
import { setConfig as setConfigAction } from "../store/configSlice";
import { setModel, setOriginalContents, newConversation } from "../store/sessionSlice";

export function useVscodeMessages() {
  const dispatch = useAppDispatch();

  useEffect(() => {
    const handler = (event: MessageEvent) => {
      const msg = event.data as { type: string; [key: string]: unknown };
      if (!msg?.type) { return; }

      switch (msg.type) {
        case "config":
          dispatch(setConfigAction({
            apiBaseUrl: msg.apiBaseUrl as string,
            model: msg.model as string,
            streamResponses: msg.streamResponses as boolean,
            autoAcceptEdits: msg.autoAcceptEdits as boolean,
          }));
          break;

        case "modelChanged":
          dispatch(setModel(msg.model as string));
          break;

        case "newConversation":
          dispatch(newConversation());
          break;

        case "codeAction":
          // Handled by ChatPage directly via a separate event
          window.dispatchEvent(new CustomEvent("codeAction", { detail: msg }));
          break;

        case "fileContent":
          // Original file content returned for diff view
          dispatch(setOriginalContents({
            toolCallId: msg.filePath as string, // matched by filePath
            originalContents: msg.content as string,
          }));
          break;

        case "workspacePath":
          // Store workspace path in localStorage for the API client
          localStorage.setItem("aiAssistant.workspacePath", JSON.stringify(msg.path));
          break;
      }
    };

    window.addEventListener("message", handler);
    return () => window.removeEventListener("message", handler);
  }, [dispatch]);
}
