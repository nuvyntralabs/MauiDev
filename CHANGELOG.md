# Changelog

## 1.2.2

- Interactive nuget.org self-update check every 4 hours (`--no-update-check` or `NUVYNTRA_NO_UPDATE_CHECK=1` to skip). The CLI does not phone home. `--no-update-check` is new in 1.2.2; Nuvyn must not pass it to `maui-dev doctor` when the installed tool is 1.2.1.

## 1.2.1

- `PackageProjectUrl` and docs links point at https://nuvyntralabs.github.io/toolkits/maui-dev/

## 1.2.0

- `publish --validate` — store ApplicationId / CFBundleIdentifier, iOS privacy manifest, pack metadata. `--push` is rejected
- `migrate` — net8/net9 TFMs, Xamarin.Forms / Essentials, Forms.Init / LoadApplication (no rewrite)
- `telemetry` — scan the app for crash / analytics SDKs. The CLI collects nothing
- `benchmark` — shells to `maui-perf` (`Plugin.Maui.Performance.Cli`); MD900 if the tool is missing

## 1.1.0

- `permissions`, `platform`, `signing`, `workload`, `version`, `dependencies`, `icons`
- `permissions --fix` deduplicates identical Android permission nodes
- `version --align` / `--bump patch|minor|major` write packable Version values (honors `--dry-run`)
- `.maui-dev.json` `ignore` accepts diagnostic ids (`MD020`) as well as check ids

## 1.0.1

- PackageId is `Plugin.Maui.MauiDev.Cli`. nuget.org reserved `MauiDev.Cli` (gallery 404, upload rejected). Command stays `maui-dev`.

## 1.0.0

- `maui-dev doctor` with machine and project checks, `--ci`, allow-listed `--fix` / `--dry-run`
- `analyze`, `resources`, `clean`, `package --validate` / `--pack`
- JSON and SARIF reporters for CI and the IDE Problems panel
- VS Code / Cursor extension `nuvyntralabs.maui-dev`
