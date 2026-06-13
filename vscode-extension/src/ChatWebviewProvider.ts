import * as vscode from "vscode";
import * as path from "path";
import * as fs from "fs";
import { logInfo, logWarn, logError } from "./logger";

export class ChatWebviewProvider implements vscode.WebviewViewProvider {
  public static readonly viewType = "ai-assistant.chatView";
  private _view?: vscode.WebviewView;

  constructor(private readonly context: vscode.ExtensionContext) {}

  resolveWebviewView(webviewView: vscode.WebviewView): void {
    this._view = webviewView;
    logInfo("ChatWebviewProvider: resolving webview view");

    // Assets are copied from gui/dist/assets → out/webview/ at build time (scripts/copy-gui.js)
    // This avoids vscode-resource:// failures caused by spaces in the project path
    const webviewUri = vscode.Uri.joinPath(this.context.extensionUri, "out", "webview");
    const isBuilt = fs.existsSync(path.join(webviewUri.fsPath, "index.js"));
    logInfo(`ChatWebviewProvider: webviewDir=${webviewUri.fsPath} isBuilt=${isBuilt}`);

    webviewView.webview.options = {
      enableScripts: true,
      localResourceRoots: [webviewUri],
    };

    const html = this._getHtml(webviewView.webview, webviewUri, isBuilt);
    logInfo(`ChatWebviewProvider: HTML length=${html.length}`);
    // Log first 500 chars of HTML to verify CSP and script tag
    logInfo(`ChatWebviewProvider: HTML preview=${html.slice(0, 500)}`);
    webviewView.webview.html = html;

    // Forward messages from webview to extension host handlers
    webviewView.webview.onDidReceiveMessage((msg) => this._handleMessage(msg));
  }

  private _getHtml(
    webview: vscode.Webview,
    webviewUri: vscode.Uri,
    isBuilt: boolean
  ): string {
    const nonce = getNonce();
    const config = vscode.workspace.getConfiguration("aiAssistant");
    const apiBaseUrl = config.get<string>("apiBaseUrl") ?? "http://localhost:5016";
    const model = config.get<string>("model") ?? "assistant-30b";

    if (!isBuilt) {
      logWarn("ChatWebviewProvider: out/webview not found — run 'npm run compile' in vscode-extension");
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

  private async _handleMessage(msg: { type: string; [key: string]: unknown }): Promise<void> {
    logInfo(`ChatWebviewProvider: received message type=${msg.type}`);
    try {
    switch (msg.type) {
      case "writeFile": {
        const { filePath, content } = msg as { filePath: string; content: string; type: string };
        logInfo(`writeFile: ${filePath}`);
        await this._writeFile(filePath, content);
        this._view?.webview.postMessage({ type: "fileWritten", filePath, success: true });
        break;
      }
      case "readFile": {
        const { filePath } = msg as { filePath: string; type: string };
        const content = await this._readFile(filePath);
        this._view?.webview.postMessage({ type: "fileContent", filePath, content });
        break;
      }
      case "openFile": {
        const { filePath } = msg as { filePath: string; type: string };
        const uri = this._resolveUri(filePath);
        if (uri) { await vscode.window.showTextDocument(uri); }
        break;
      }
      case "showDiff": {
        const { filePath, originalContent, newContent } = msg as {
          filePath: string; originalContent: string; newContent: string; type: string;
        };
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
          autoAcceptEdits: config.get("autoAcceptEdits") ?? false,
        });
        break;
      }
      case "getWorkspacePath": {
        const folders = vscode.workspace.workspaceFolders;
        this._view?.webview.postMessage({
          type: "workspacePath",
          path: folders?.[0]?.uri.fsPath ?? "",
        });
        break;
      }
      case "getSelectedCode": {
        const editor = vscode.window.activeTextEditor;
        if (editor && !editor.selection.isEmpty) {
          const text = editor.document.getText(editor.selection);
          const lang = editor.document.languageId;
          const file = path.basename(editor.document.fileName);
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

  private async _writeFile(filePath: string, content: string): Promise<void> {
    const uri = this._resolveUri(filePath);
    if (!uri) { throw new Error(`Cannot resolve path: ${filePath}`); }
    const dir = vscode.Uri.file(path.dirname(uri.fsPath));
    await vscode.workspace.fs.createDirectory(dir);
    await vscode.workspace.fs.writeFile(uri, Buffer.from(content, "utf8"));
  }

  private async _readFile(filePath: string): Promise<string> {
    const uri = this._resolveUri(filePath);
    if (!uri) { return ""; }
    try {
      const bytes = await vscode.workspace.fs.readFile(uri);
      return Buffer.from(bytes).toString("utf8");
    } catch {
      return "";
    }
  }

  private async _showDiff(filePath: string, originalContent: string, newContent: string): Promise<void> {
    const lang = filePath.split(".").pop() ?? "plaintext";
    const originalDoc = await vscode.workspace.openTextDocument({ content: originalContent, language: lang });
    const proposedDoc = await vscode.workspace.openTextDocument({ content: newContent, language: lang });
    await vscode.commands.executeCommand(
      "vscode.diff",
      originalDoc.uri,
      proposedDoc.uri,
      `AI Proposal: ${path.basename(filePath)} (original ↔ proposed)`
    );
  }

  private _resolveUri(filePath: string): vscode.Uri | undefined {
    if (path.isAbsolute(filePath)) { return vscode.Uri.file(filePath); }
    const folders = vscode.workspace.workspaceFolders;
    if (!folders) { return undefined; }
    return vscode.Uri.file(path.join(folders[0].uri.fsPath, filePath));
  }

  // Public API for commands
  public postMessage(msg: unknown): void {
    this._view?.webview.postMessage(msg);
  }

  public focus(): void {
    vscode.commands.executeCommand("ai-assistant.chatView.focus");
  }
}

function getNonce(): string {
  let text = "";
  const chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
  for (let i = 0; i < 32; i++) {
    text += chars.charAt(Math.floor(Math.random() * chars.length));
  }
  return text;
}
