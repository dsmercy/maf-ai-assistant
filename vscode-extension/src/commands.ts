import * as vscode from "vscode";
import { ChatWebviewProvider } from "./ChatWebviewProvider";

export function registerCommands(
  context: vscode.ExtensionContext,
  provider: ChatWebviewProvider
): void {
  const reg = (cmd: string, fn: () => void) =>
    context.subscriptions.push(vscode.commands.registerCommand(cmd, fn));

  reg("ai-assistant.focusChat", () => provider.focus());

  reg("ai-assistant.newConversation", () =>
    provider.postMessage({ type: "newConversation" })
  );

  reg("ai-assistant.switchModel", () =>
    provider.postMessage({ type: "requestSwitchModel" })
  );

  // Code actions — send selected code + prompt to chat
  const codeAction = (prompt: string) => {
    const editor = vscode.window.activeTextEditor;
    if (!editor || editor.selection.isEmpty) {
      vscode.window.showWarningMessage("AI Assistant: Select some code first.");
      return;
    }
    const code = editor.document.getText(editor.selection);
    const lang = editor.document.languageId;
    provider.focus();
    provider.postMessage({ type: "codeAction", prompt, code, lang });
  };

  reg("ai-assistant.reviewCode",    () => codeAction("Review this code for bugs, performance issues, and best practices:"));
  reg("ai-assistant.explainCode",   () => codeAction("Explain what this code does in detail:"));
  reg("ai-assistant.generateTests", () => codeAction("Generate comprehensive unit tests for this code:"));
  reg("ai-assistant.fixCode",       () => codeAction("Find and fix any bugs or issues in this code:"));
  reg("ai-assistant.documentCode",  () => codeAction("Add XML documentation comments to this code:"));
}
