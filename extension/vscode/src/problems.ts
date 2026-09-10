import * as path from "path";
import * as vscode from "vscode";

export interface DiagnosticDto {
  id: string;
  message: string;
  file?: string;
  line?: number;
  severity: "pass" | "warn" | "fail" | "skip";
  why?: string;
  nextStep?: string;
}

export interface ResultDto {
  id: string;
  title: string;
  category: string;
  status: "pass" | "warn" | "fail" | "skip";
  detail?: string;
  recommendation?: string;
  diagnostics: DiagnosticDto[];
}

export interface Report {
  command: string;
  exitCode: number;
  results: ResultDto[];
  recommendations: string[];
}

export function applyProblems(collection: vscode.DiagnosticCollection, report: Report, workspace: vscode.Uri): void {
  collection.clear();
  const grouped = new Map<string, vscode.Diagnostic[]>();

  for (const result of report.results) {
    const items = result.diagnostics.length > 0 ? result.diagnostics : fallback(result);
    for (const item of items) {
      if (item.severity === "pass" || item.severity === "skip") {
        continue;
      }
      const file = item.file ? resolve(workspace, item.file) : workspace;
      const line = Math.max(0, (item.line ?? 1) - 1);
      const range = new vscode.Range(line, 0, line, 200);
      const message = item.nextStep ? `${item.message} — ${item.nextStep}` : item.message;
      const diagnostic = new vscode.Diagnostic(
        range,
        message,
        item.severity === "fail" ? vscode.DiagnosticSeverity.Error : vscode.DiagnosticSeverity.Warning
      );
      diagnostic.source = "maui-dev";
      diagnostic.code = item.id;
      const list = grouped.get(file.toString()) ?? [];
      list.push(diagnostic);
      grouped.set(file.toString(), list);
    }
  }

  for (const [uri, diagnostics] of grouped) {
    collection.set(vscode.Uri.parse(uri), diagnostics);
  }
}

function fallback(result: ResultDto): DiagnosticDto[] {
  if (result.status === "pass" || result.status === "skip") {
    return [];
  }
  return [
    {
      id: result.id,
      message: result.detail ?? result.title,
      severity: result.status,
      nextStep: result.recommendation
    }
  ];
}

function resolve(workspace: vscode.Uri, file: string): vscode.Uri {
  return path.isAbsolute(file) ? vscode.Uri.file(file) : vscode.Uri.joinPath(workspace, file);
}
