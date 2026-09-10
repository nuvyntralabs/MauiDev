import * as vscode from "vscode";
import type { Report } from "./problems";

export function showReport(report: Report): void {
  const panel = vscode.window.createWebviewPanel("mauiDevReport", "MauiDev", vscode.ViewColumn.Beside, {});
  const rows = report.results
    .map((result) => {
      const mark = result.status === "pass" ? "✓" : result.status === "warn" ? "⚠" : result.status === "fail" ? "✗" : "·";
      return `<tr><td>${escapeHtml(mark)}</td><td>${escapeHtml(result.title)}</td><td>${escapeHtml(result.detail ?? result.status)}</td></tr>`;
    })
    .join("");
  const recs = report.recommendations.map((item, index) => `<li>[${index + 1}] ${escapeHtml(item)}</li>`).join("");
  panel.webview.html = `<!DOCTYPE html>
<html>
<body style="font-family: var(--vscode-font-family); color: var(--vscode-foreground); padding: 1rem;">
  <h2>.NET MAUI Developer Doctor</h2>
  <p>Command: ${escapeHtml(report.command)}</p>
  <table cellpadding="6">
    <tr><th></th><th>Check</th><th>Detail</th></tr>
    ${rows}
  </table>
  <h3>Recommendations</h3>
  <ol>${recs || "<li>None</li>"}</ol>
</body>
</html>`;
}

function escapeHtml(value: string): string {
  return value.replace(/[&<>"']/g, (ch) => ({ "&": "&amp;", "<": "&lt;", ">": "&gt;", '"': "&quot;", "'": "&#39;" }[ch] ?? ch));
}
