import * as fs from "fs";
import * as path from "path";

type LogLevel = "INFO" | "WARN" | "ERROR";

let logFilePath: string | null = null;

export function initLogger(extensionPath: string): void {
  const logsDir = path.join(extensionPath, "Logs");
  if (!fs.existsSync(logsDir)) {
    fs.mkdirSync(logsDir, { recursive: true });
  }
  const timestamp = new Date().toISOString().replace(/[:.]/g, "-").slice(0, 19);
  logFilePath = path.join(logsDir, `ai-assistant-${timestamp}.log`);
  log("INFO", "Logger initialised — log file: " + logFilePath);
}

export function log(level: LogLevel, message: string, error?: unknown): void {
  const ts = new Date().toISOString();
  let line = `[${ts}] [${level}] ${message}`;
  if (error !== undefined) {
    line += "\n" + formatError(error);
  }
  console.log(line);
  if (logFilePath) {
    try {
      fs.appendFileSync(logFilePath, line + "\n");
    } catch {
      // If file write fails don't crash the extension
    }
  }
}

export function logInfo(message: string): void  { log("INFO",  message); }
export function logWarn(message: string): void  { log("WARN",  message); }
export function logError(message: string, error?: unknown): void {
  log("ERROR", message, error);
}

function formatError(error: unknown): string {
  if (error instanceof Error) {
    return `  ${error.name}: ${error.message}${error.stack ? "\n  Stack: " + error.stack : ""}`;
  }
  return `  ${String(error)}`;
}
