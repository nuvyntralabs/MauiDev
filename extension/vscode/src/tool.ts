import * as cp from "child_process";
import * as fs from "fs";
import * as os from "os";
import * as path from "path";
import * as vscode from "vscode";
import type { Report } from "./problems";

export async function resolveToolPath(): Promise<string | undefined> {
  const configured = vscode.workspace.getConfiguration("mauiDev").get<string>("toolPath")?.trim();
  if (configured && fs.existsSync(configured)) {
    return configured;
  }

  const home = os.homedir();
  const names = process.platform === "win32" ? ["maui-dev.exe", "maui-dev"] : ["maui-dev"];
  const dirs = [
    path.join(home, ".dotnet", "tools"),
    ...(process.env.PATH ?? "").split(path.delimiter)
  ];
  for (const dir of dirs) {
    for (const name of names) {
      const candidate = path.join(dir, name);
      if (fs.existsSync(candidate)) {
        return candidate;
      }
    }
  }

  return undefined;
}

export async function installMauiDevTool(): Promise<void> {
  await vscode.window.withProgress(
    { location: vscode.ProgressLocation.Notification, title: "Installing MauiDev.Cli" },
    () =>
      new Promise<void>((resolve, reject) => {
        cp.exec("dotnet tool install -g MauiDev.Cli", (error, stdout, stderr) => {
          if (error) {
            reject(new Error(stderr || error.message));
            return;
          }
          void vscode.window.showInformationMessage(stdout.trim() || "MauiDev.Cli installed.");
          resolve();
        });
      })
  );
}

export function runMauiDev(tool: string, command: string, extra: string[], cwd: string): Promise<Report> {
  const warnAsError = vscode.workspace.getConfiguration("mauiDev").get<boolean>("warnAsError") === true;
  const args = [command, "--format", "json", "--path", cwd, ...extra];
  if (warnAsError) {
    args.push("--warn-as-error");
  }

  return new Promise((resolve, reject) => {
    cp.execFile(tool, args, { cwd, maxBuffer: 8 * 1024 * 1024 }, (error, stdout, stderr) => {
      const json = extractJson(stdout);
      if (!json) {
        reject(new Error(stderr || (error ? error.message : "maui-dev produced no JSON.")));
        return;
      }
      try {
        resolve(JSON.parse(json) as Report);
      } catch (parseError) {
        reject(parseError instanceof Error ? parseError : new Error(String(parseError)));
      }
    });
  });
}

function extractJson(stdout: string): string | undefined {
  const start = stdout.indexOf("{");
  if (start < 0) {
    return undefined;
  }
  return stdout.slice(start);
}
