import * as vscode from "vscode";
import { runMauiDev, installMauiDevTool, resolveToolPath } from "./tool";
import { applyProblems, type Report } from "./problems";
import { showReport } from "./report";

let lastReport: Report | undefined;
let status: vscode.StatusBarItem;
const collection = vscode.languages.createDiagnosticCollection("maui-dev");

export function activate(context: vscode.ExtensionContext): void {
  status = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, 100);
  status.command = "mauiDev.showReport";
  status.text = "MauiDev";
  status.show();

  context.subscriptions.push(
    collection,
    status,
    vscode.commands.registerCommand("mauiDev.doctor", () => run("doctor")),
    vscode.commands.registerCommand("mauiDev.analyze", () => run("analyze")),
    vscode.commands.registerCommand("mauiDev.resources", () => run("resources")),
    vscode.commands.registerCommand("mauiDev.clean", () => run("clean")),
    vscode.commands.registerCommand("mauiDev.package", () => run("package", ["--validate"])),
    vscode.commands.registerCommand("mauiDev.showReport", () => {
      if (!lastReport) {
        void vscode.window.showInformationMessage("Run MauiDev: Doctor first.");
        return;
      }
      showReport(lastReport);
    }),
    vscode.commands.registerCommand("mauiDev.installTool", () => installMauiDevTool())
  );

  const auto = vscode.workspace.getConfiguration("mauiDev").get<boolean>("autoDoctorOnOpen") === true;
  if (auto) {
    void run("doctor");
  }
}

export function deactivate(): void {
  collection.dispose();
}

async function run(command: string, extra: string[] = []): Promise<void> {
  const folder = vscode.workspace.workspaceFolders?.[0];
  if (!folder) {
    void vscode.window.showErrorMessage("Open a folder before running MauiDev.");
    return;
  }

  const tool = await resolveToolPath();
  if (!tool) {
    const choice = await vscode.window.showErrorMessage(
      "maui-dev was not found. Install MauiDev.Cli?",
      "Install",
      "Cancel"
    );
    if (choice === "Install") {
      await installMauiDevTool();
    }
    return;
  }

  status.text = `MauiDev: ${command}…`;
  try {
    const report = await runMauiDev(tool, command, extra, folder.uri.fsPath);
    lastReport = report;
    applyProblems(collection, report, folder.uri);
    const failed = report.results.filter((item) => item.status === "fail").length;
    const warnings = report.results.filter((item) => item.status === "warn").length;
    status.text = `MauiDev: ${failed} failed · ${warnings} warnings`;
    if (command === "doctor") {
      showReport(report);
    }
  } catch (error) {
    status.text = "MauiDev: error";
    void vscode.window.showErrorMessage(error instanceof Error ? error.message : String(error));
  }
}
