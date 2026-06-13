"use strict";
var __create = Object.create;
var __defProp = Object.defineProperty;
var __getOwnPropDesc = Object.getOwnPropertyDescriptor;
var __getOwnPropNames = Object.getOwnPropertyNames;
var __getProtoOf = Object.getPrototypeOf;
var __hasOwnProp = Object.prototype.hasOwnProperty;
var __export = (target, all) => {
  for (var name in all)
    __defProp(target, name, { get: all[name], enumerable: true });
};
var __copyProps = (to, from, except, desc) => {
  if (from && typeof from === "object" || typeof from === "function") {
    for (let key of __getOwnPropNames(from))
      if (!__hasOwnProp.call(to, key) && key !== except)
        __defProp(to, key, { get: () => from[key], enumerable: !(desc = __getOwnPropDesc(from, key)) || desc.enumerable });
  }
  return to;
};
var __toESM = (mod, isNodeMode, target) => (target = mod != null ? __create(__getProtoOf(mod)) : {}, __copyProps(
  // If the importer is in node compatibility mode or this is not an ESM
  // file that has been converted to a CommonJS file using a Babel-
  // compatible transform (i.e. "__esModule" has not been set), then set
  // "default" to the CommonJS "module.exports" for node compatibility.
  isNodeMode || !mod || !mod.__esModule ? __defProp(target, "default", { value: mod, enumerable: true }) : target,
  mod
));
var __toCommonJS = (mod) => __copyProps(__defProp({}, "__esModule", { value: true }), mod);

// src/extension.ts
var extension_exports = {};
__export(extension_exports, {
  activate: () => activate,
  deactivate: () => deactivate
});
module.exports = __toCommonJS(extension_exports);
var vscode3 = __toESM(require("vscode"));

// src/ChatWebviewProvider.ts
var vscode = __toESM(require("vscode"));
var path2 = __toESM(require("path"));
var fs2 = __toESM(require("fs"));

// src/logger.ts
var fs = __toESM(require("fs"));
var path = __toESM(require("path"));
var logFilePath = null;
function initLogger(extensionPath) {
  const logsDir = path.join(extensionPath, "Logs");
  if (!fs.existsSync(logsDir)) {
    fs.mkdirSync(logsDir, { recursive: true });
  }
  const timestamp = (/* @__PURE__ */ new Date()).toISOString().replace(/[:.]/g, "-").slice(0, 19);
  logFilePath = path.join(logsDir, `ai-assistant-${timestamp}.log`);
  log("INFO", "Logger initialised \u2014 log file: " + logFilePath);
}
function log(level, message, error) {
  const ts = (/* @__PURE__ */ new Date()).toISOString();
  let line = `[${ts}] [${level}] ${message}`;
  if (error !== void 0) {
    line += "\n" + formatError(error);
  }
  console.log(line);
  if (logFilePath) {
    try {
      fs.appendFileSync(logFilePath, line + "\n");
    } catch {
    }
  }
}
function logInfo(message) {
  log("INFO", message);
}
function logWarn(message) {
  log("WARN", message);
}
function logError(message, error) {
  log("ERROR", message, error);
}
function formatError(error) {
  if (error instanceof Error) {
    return `  ${error.name}: ${error.message}${error.stack ? "\n  Stack: " + error.stack : ""}`;
  }
  return `  ${String(error)}`;
}

// src/ChatWebviewProvider.ts
var ChatWebviewProvider = class {
  constructor(context) {
    this.context = context;
  }
  static {
    this.viewType = "ai-assistant.chatView";
  }
  resolveWebviewView(webviewView) {
    this._view = webviewView;
    logInfo("ChatWebviewProvider: resolving webview view");
    const webviewUri = vscode.Uri.joinPath(this.context.extensionUri, "out", "webview");
    const isBuilt = fs2.existsSync(path2.join(webviewUri.fsPath, "index.js"));
    logInfo(`ChatWebviewProvider: webviewDir=${webviewUri.fsPath} isBuilt=${isBuilt}`);
    webviewView.webview.options = {
      enableScripts: true,
      localResourceRoots: [webviewUri]
    };
    const html = this._getHtml(webviewView.webview, webviewUri, isBuilt);
    logInfo(`ChatWebviewProvider: HTML length=${html.length}`);
    logInfo(`ChatWebviewProvider: HTML preview=${html.slice(0, 500)}`);
    webviewView.webview.html = html;
    webviewView.webview.onDidReceiveMessage((msg) => this._handleMessage(msg));
  }
  _getHtml(webview, webviewUri, isBuilt) {
    const nonce = getNonce();
    const config = vscode.workspace.getConfiguration("aiAssistant");
    const apiBaseUrl = config.get("apiBaseUrl") ?? "http://localhost:5016";
    const model = config.get("model") ?? "assistant-30b";
    if (!isBuilt) {
      logWarn("ChatWebviewProvider: out/webview not found \u2014 run 'npm run compile' in vscode-extension");
      return `<!DOCTYPE html><html><body><p style="color:orange;padding:12px;font-family:sans-serif">
        AI Assistant: GUI not built.<br>Run <code>npm run compile</code> in the vscode-extension folder then reload.
      </p></body></html>`;
    }
    const scriptUri = webview.asWebviewUri(vscode.Uri.joinPath(webviewUri, "index.js"));
    logInfo(`ChatWebviewProvider: scriptUri=${scriptUri}`);
    logInfo(`ChatWebviewProvider: cspSource=${webview.cspSource}`);
    return `<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <meta http-equiv="Content-Security-Policy" content="default-src 'none'; style-src ${webview.cspSource} 'unsafe-inline'; script-src 'nonce-${nonce}' ${webview.cspSource}; connect-src ${apiBaseUrl} https:; img-src ${webview.cspSource} data:; font-src ${webview.cspSource} data:;">
  <title>AI Assistant</title>
</head>
<body>
  <div id="root"></div>
  <script nonce="${nonce}">
    localStorage.setItem("aiAssistant.apiBaseUrl", JSON.stringify("${apiBaseUrl}"));
    localStorage.setItem("aiAssistant.model", JSON.stringify("${model}"));
  </script>
  <script nonce="${nonce}" src="${scriptUri}"></script>
</body>
</html>`;
  }
  async _handleMessage(msg) {
    logInfo(`ChatWebviewProvider: received message type=${msg.type}`);
    try {
      switch (msg.type) {
        case "writeFile": {
          const { filePath, content } = msg;
          logInfo(`writeFile: ${filePath}`);
          await this._writeFile(filePath, content);
          this._view?.webview.postMessage({ type: "fileWritten", filePath, success: true });
          break;
        }
        case "readFile": {
          const { filePath } = msg;
          const content = await this._readFile(filePath);
          this._view?.webview.postMessage({ type: "fileContent", filePath, content });
          break;
        }
        case "openFile": {
          const { filePath } = msg;
          const uri = this._resolveUri(filePath);
          if (uri) {
            await vscode.window.showTextDocument(uri);
          }
          break;
        }
        case "showDiff": {
          const { filePath, originalContent, newContent } = msg;
          await this._showDiff(filePath, originalContent, newContent);
          break;
        }
        case "getConfig": {
          const config = vscode.workspace.getConfiguration("aiAssistant");
          this._view?.webview.postMessage({
            type: "config",
            apiBaseUrl: config.get("apiBaseUrl") ?? "http://localhost:5016",
            model: config.get("model") ?? "assistant-30b",
            streamResponses: config.get("streamResponses") ?? true,
            autoAcceptEdits: config.get("autoAcceptEdits") ?? false
          });
          break;
        }
        case "getWorkspacePath": {
          const folders = vscode.workspace.workspaceFolders;
          this._view?.webview.postMessage({
            type: "workspacePath",
            path: folders?.[0]?.uri.fsPath ?? ""
          });
          break;
        }
        case "getSelectedCode": {
          const editor = vscode.window.activeTextEditor;
          if (editor && !editor.selection.isEmpty) {
            const text = editor.document.getText(editor.selection);
            const lang = editor.document.languageId;
            const file = path2.basename(editor.document.fileName);
            this._view?.webview.postMessage({ type: "selectedCode", text, lang, file });
          }
          break;
        }
        case "switchModel": {
          const models = ["assistant-30b", "assistant-14b", "ai-assistant"];
          const picked = await vscode.window.showQuickPick(models, { placeHolder: "Select model" });
          if (picked) {
            await vscode.workspace.getConfiguration("aiAssistant").update("model", picked, true);
            this._view?.webview.postMessage({ type: "modelChanged", model: picked });
          }
          break;
        }
      }
    } catch (err) {
      logError(`ChatWebviewProvider: error handling message type=${msg.type}`, err);
      this._view?.webview.postMessage({ type: "error", message: String(err) });
    }
  }
  async _writeFile(filePath, content) {
    const uri = this._resolveUri(filePath);
    if (!uri) {
      throw new Error(`Cannot resolve path: ${filePath}`);
    }
    const dir = vscode.Uri.file(path2.dirname(uri.fsPath));
    await vscode.workspace.fs.createDirectory(dir);
    await vscode.workspace.fs.writeFile(uri, Buffer.from(content, "utf8"));
  }
  async _readFile(filePath) {
    const uri = this._resolveUri(filePath);
    if (!uri) {
      return "";
    }
    try {
      const bytes = await vscode.workspace.fs.readFile(uri);
      return Buffer.from(bytes).toString("utf8");
    } catch {
      return "";
    }
  }
  async _showDiff(filePath, originalContent, newContent) {
    const lang = filePath.split(".").pop() ?? "plaintext";
    const originalDoc = await vscode.workspace.openTextDocument({ content: originalContent, language: lang });
    const proposedDoc = await vscode.workspace.openTextDocument({ content: newContent, language: lang });
    await vscode.commands.executeCommand(
      "vscode.diff",
      originalDoc.uri,
      proposedDoc.uri,
      `AI Proposal: ${path2.basename(filePath)} (original \u2194 proposed)`
    );
  }
  _resolveUri(filePath) {
    if (path2.isAbsolute(filePath)) {
      return vscode.Uri.file(filePath);
    }
    const folders = vscode.workspace.workspaceFolders;
    if (!folders) {
      return void 0;
    }
    return vscode.Uri.file(path2.join(folders[0].uri.fsPath, filePath));
  }
  // Public API for commands
  postMessage(msg) {
    this._view?.webview.postMessage(msg);
  }
  focus() {
    vscode.commands.executeCommand("ai-assistant.chatView.focus");
  }
};
function getNonce() {
  let text = "";
  const chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
  for (let i = 0; i < 32; i++) {
    text += chars.charAt(Math.floor(Math.random() * chars.length));
  }
  return text;
}

// src/commands.ts
var vscode2 = __toESM(require("vscode"));
function registerCommands(context, provider) {
  const reg = (cmd, fn) => context.subscriptions.push(vscode2.commands.registerCommand(cmd, fn));
  reg("ai-assistant.focusChat", () => provider.focus());
  reg(
    "ai-assistant.newConversation",
    () => provider.postMessage({ type: "newConversation" })
  );
  reg(
    "ai-assistant.switchModel",
    () => provider.postMessage({ type: "requestSwitchModel" })
  );
  const codeAction = (prompt) => {
    const editor = vscode2.window.activeTextEditor;
    if (!editor || editor.selection.isEmpty) {
      vscode2.window.showWarningMessage("AI Assistant: Select some code first.");
      return;
    }
    const code = editor.document.getText(editor.selection);
    const lang = editor.document.languageId;
    provider.focus();
    provider.postMessage({ type: "codeAction", prompt, code, lang });
  };
  reg("ai-assistant.reviewCode", () => codeAction("Review this code for bugs, performance issues, and best practices:"));
  reg("ai-assistant.explainCode", () => codeAction("Explain what this code does in detail:"));
  reg("ai-assistant.generateTests", () => codeAction("Generate comprehensive unit tests for this code:"));
  reg("ai-assistant.fixCode", () => codeAction("Find and fix any bugs or issues in this code:"));
  reg("ai-assistant.documentCode", () => codeAction("Add XML documentation comments to this code:"));
}

// src/extension.ts
function activate(context) {
  initLogger(context.extensionPath);
  logInfo("AI Coding Assistant activating");
  process.on("uncaughtException", (err) => {
    const stack = err?.stack ?? "";
    if (stack.includes("extensions\\copilot") || stack.includes("extensions/copilot")) {
      return;
    }
    logError("Uncaught exception", err);
  });
  process.on("unhandledRejection", (reason) => {
    const stack = (reason instanceof Error ? reason.stack : String(reason)) ?? "";
    if (stack.includes("extensions\\copilot") || stack.includes("extensions/copilot")) {
      return;
    }
    logError("Unhandled promise rejection", reason);
  });
  try {
    const provider = new ChatWebviewProvider(context);
    context.subscriptions.push(
      vscode3.window.registerWebviewViewProvider(
        ChatWebviewProvider.viewType,
        provider,
        { webviewOptions: { retainContextWhenHidden: true } }
      )
    );
    registerCommands(context, provider);
    const statusBar = vscode3.window.createStatusBarItem(vscode3.StatusBarAlignment.Right, 100);
    statusBar.command = "ai-assistant.switchModel";
    statusBar.tooltip = "Click to switch AI Assistant model";
    const updateStatusBar = () => {
      const model = vscode3.workspace.getConfiguration("aiAssistant").get("model") ?? "assistant-30b";
      statusBar.text = `$(hubot) ${model}`;
      statusBar.show();
    };
    updateStatusBar();
    context.subscriptions.push(
      statusBar,
      vscode3.workspace.onDidChangeConfiguration((e) => {
        if (e.affectsConfiguration("aiAssistant.model")) {
          updateStatusBar();
        }
      })
    );
    logInfo("AI Coding Assistant activated successfully");
  } catch (err) {
    logError("Failed to activate AI Coding Assistant", err);
    vscode3.window.showErrorMessage(`AI Assistant failed to activate: ${err instanceof Error ? err.message : String(err)}`);
  }
}
function deactivate() {
  logInfo("AI Coding Assistant deactivated");
}
// Annotate the CommonJS export names for ESM import in node:
0 && (module.exports = {
  activate,
  deactivate
});
//# sourceMappingURL=extension.js.map
