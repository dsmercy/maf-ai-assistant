import * as vscode from "vscode";
import { ChatWebviewProvider } from "./ChatWebviewProvider";
import { registerCommands } from "./commands";
import { initLogger, logInfo, logWarn, logError } from "./logger";

export function activate(context: vscode.ExtensionContext): void {
  initLogger(context.extensionPath);
  logInfo("AI Coding Assistant activating");

  // Catch unhandled promise rejections and exceptions in the extension host
  // Filter out errors from other extensions (e.g. Copilot) to keep our log clean
  process.on("uncaughtException", (err) => {
    const stack = err?.stack ?? "";
    if (stack.includes("extensions\\copilot") || stack.includes("extensions/copilot")) { return; }
    logError("Uncaught exception", err);
  });
  process.on("unhandledRejection", (reason) => {
    const stack = (reason instanceof Error ? reason.stack : String(reason)) ?? "";
    if (stack.includes("extensions\\copilot") || stack.includes("extensions/copilot")) { return; }
    logError("Unhandled promise rejection", reason);
  });

  try {
    const provider = new ChatWebviewProvider(context);

    context.subscriptions.push(
      vscode.window.registerWebviewViewProvider(
        ChatWebviewProvider.viewType,
        provider,
        { webviewOptions: { retainContextWhenHidden: true } }
      )
    );

    registerCommands(context, provider);

    // Status bar — shows current model, click to switch
    const statusBar = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Right, 100);
    statusBar.command = "ai-assistant.switchModel";
    statusBar.tooltip = "Click to switch AI Assistant model";

    const updateStatusBar = () => {
      const model = vscode.workspace.getConfiguration("aiAssistant").get<string>("model") ?? "assistant-30b";
      statusBar.text = `$(hubot) ${model}`;
      statusBar.show();
    };

    updateStatusBar();
    context.subscriptions.push(
      statusBar,
      vscode.workspace.onDidChangeConfiguration((e) => {
        if (e.affectsConfiguration("aiAssistant.model")) { updateStatusBar(); }
      })
    );

    logInfo("AI Coding Assistant activated successfully");
  } catch (err) {
    logError("Failed to activate AI Coding Assistant", err);
    vscode.window.showErrorMessage(`AI Assistant failed to activate: ${err instanceof Error ? err.message : String(err)}`);
  }
}

export function deactivate(): void {
  logInfo("AI Coding Assistant deactivated");
}
