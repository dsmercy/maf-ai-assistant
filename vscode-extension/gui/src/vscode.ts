// Bridge to the VS Code extension host via acquireVsCodeApi()
// In dev mode (browser), we stub the API so the UI still renders.

export interface VsCodeApi {
  postMessage(msg: unknown): void;
  getState(): unknown;
  setState(state: unknown): void;
}

declare function acquireVsCodeApi(): VsCodeApi;

function createVsCodeApi(): VsCodeApi {
  if (typeof acquireVsCodeApi !== "undefined") {
    return acquireVsCodeApi();
  }
  // Browser dev-mode stub
  return {
    postMessage: (msg) => console.log("[vscode stub] postMessage", msg),
    getState: () => ({}),
    setState: () => {},
  };
}

export const vscodeApi = createVsCodeApi();

export function postToExtension(msg: { type: string; [key: string]: unknown }): void {
  vscodeApi.postMessage(msg);
}
