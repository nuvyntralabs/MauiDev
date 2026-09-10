# MauiDev

Developer productivity toolkit for .NET MAUI: a `maui-dev` [dotnet tool](https://learn.microsoft.com/dotnet/core/tools/global-tools) plus a VS Code / Cursor extension.

**GitHub:** https://github.com/nuvyntralabs/MauiDev  
**NuGet:** https://www.nuget.org/packages/MauiDev.Cli  
**Catalog:** https://github.com/nuvyntralabs/MauiEssentials  
**Author:** [Niladri Prasad Padhy](https://github.com/NiladriPadhy)  
**License:** MIT

Usual alternatives: [maui-check](https://github.com/Redth/dotnet-maui-check) (environment only), `dotnet workload`, Visual Studio’s MAUI installer, and the Microsoft `maui` CLI. MauiDev is project-aware: it reads csproj, manifests, and MAUI resource items, can apply a small allow-list of fixes, and emits JSON/SARIF for CI and the IDE Problems panel.

It does **not** replace runtime plugins. Use `Plugin.Maui.Performance` / `maui-perf` for traces, `Plugin.Maui.LeakAnalyser` for visual-tree leaks, `Plugin.Maui.AppHealth` for device health, and `Plugin.Maui.Diagnostics` for crashes.

## Install

```bash
dotnet tool install -g MauiDev.Cli
maui-dev doctor
```

VS Code / Cursor: install the **MauiDev** extension (`nuvyntralabs.maui-dev`) from the Marketplace or Open VSX. The extension shells out to `maui-dev` and offers to install the tool if it is missing.

## Commands (1.0)

| Command | Purpose |
| --- | --- |
| `maui-dev doctor` | SDK, workloads, Android SDK, JDK, Xcode, CocoaPods, TFMs, min SDK, permissions, duplicate resources, UseMaui, signing |
| `maui-dev analyze` | Cheap C# heuristics (event retention, HttpClient, fire-and-forget, MainThread hops, platform guards) |
| `maui-dev resources` | Duplicate / missing / unused MauiImage, splash, font |
| `maui-dev clean` | Delete `bin` / `obj` (optional NuGet HTTP cache and workload temp behind flags) |
| `maui-dev package` | Validate pack metadata (`--validate`, default). `--pack` runs `dotnet pack` locally and never pushes |

Global options: `--path`, `--format human|json|sarif`, `--ci` (JSON + warn-as-error), `--fix`, `--dry-run`, `--warn-as-error`, `--timeout`.

```bash
maui-dev doctor --fix --dry-run
maui-dev analyze --ci
maui-dev package --validate
```

`doctor --fix` only:

- Deduplicates identical `MauiSplashScreen` / `MauiImage` / `MauiIcon` / `MauiFont` items
- Inserts `<UseMaui>true</UseMaui>` when the project already looks like MAUI

It never bumps min SDK, removes permissions, writes signing secrets, installs workloads, or publishes NuGet packages.

Exit codes: `0` pass/skip, `1` fail (or warning with `--warn-as-error` / `--ci`), `2` usage error.

## CI

```yaml
- script: maui-dev doctor --ci
- script: maui-dev analyze --ci
- script: maui-dev package --validate --ci
```

Publishing `MauiDev.Cli` is pipeline-only on this repository. nuget.org uses the Actions secret `NUGET_KEY-MauiDev-Cli`. Do not run `dotnet nuget push` from a local clone.

## Later (not 1.0)

`permissions`, `platform`, `signing`, `publish`, `workload`, `version`, `dependencies`, `migrate`, `icons`, `telemetry`, `benchmark` (will shell to `maui-perf`), plus `MauiDev.Analyzers` and `MauiDev.Templates`.
