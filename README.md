# MauiDev

Developer productivity toolkit for .NET MAUI: a `maui-dev` [dotnet tool](https://learn.microsoft.com/dotnet/core/tools/global-tools) plus a VS Code / Cursor extension.

**GitHub:** https://github.com/nuvyntralabs/MauiDev  
**NuGet:** https://www.nuget.org/packages/Plugin.Maui.MauiDev.Cli  
**Docs:** https://nuvyntralabs.github.io/toolkits/maui-dev/  
**Catalog:** https://github.com/nuvyntralabs/MauiEssentials  
**Author:** [Niladri Prasad Padhy](https://github.com/NiladriPadhy)  
**License:** MIT

Usual alternatives: [maui-check](https://github.com/Redth/dotnet-maui-check) (environment only), `dotnet workload`, Visual Studio’s MAUI installer, and the Microsoft `maui` CLI. MauiDev is project-aware: it reads csproj, manifests, and MAUI resource items, can apply a small allow-list of fixes, and emits JSON/SARIF for CI and the IDE Problems panel.

It does **not** replace runtime plugins. Use `Plugin.Maui.Performance` / `maui-perf` for traces, `Plugin.Maui.LeakAnalyser` for visual-tree leaks, `Plugin.Maui.AppHealth` for device health, and `Plugin.Maui.Diagnostics` for crashes.

## Install

```bash
dotnet tool install -g Plugin.Maui.MauiDev.Cli --source https://api.nuget.org/v3/index.json
maui-dev doctor
```

VS Code / Cursor: install the **MauiDev** extension (`nuvyntralabs.maui-dev`) from the Marketplace or Open VSX. The extension shells out to `maui-dev` and offers to install the tool if it is missing.

## Commands (1.2)

| Command | Purpose |
| --- | --- |
| `maui-dev doctor` | SDK, workloads, Android SDK, JDK, Xcode, CocoaPods, TFMs, min SDK, permissions, duplicate resources, UseMaui, signing |
| `maui-dev analyze` | Cheap C# heuristics (event retention, HttpClient, fire-and-forget, MainThread hops, platform guards) |
| `maui-dev resources` | Duplicate / missing / unused MauiImage, splash, font |
| `maui-dev permissions` | Android unused/duplicate permissions, iOS usage strings, Android 13+ media/notification |
| `maui-dev platform` | TFM ↔ `Platforms/` folders, shared-code guards, Windows/Catalyst note, min OS |
| `maui-dev signing` | Keystore / entitlements checklist. Never writes secrets |
| `maui-dev workload` | Diagnose the MAUI workload and print the install command. Never installs |
| `maui-dev version` | Align packable `Version` values (`--align`) or `--bump patch\|minor\|major` |
| `maui-dev dependencies` | PackageReference duplicates, Maui.Controls drift, CPM clash, tool-as-library |
| `maui-dev icons` | MauiIcon / splash presence, adaptive background, iOS 1024 marketing size |
| `maui-dev publish` | Validate store ApplicationId / CFBundleIdentifier, iOS privacy manifest, pack metadata. Never pushes |
| `maui-dev migrate` | Flag `net8`/`net9` TFMs, Xamarin.Forms / Essentials, `Forms.Init` / `LoadApplication` |
| `maui-dev telemetry` | Scan the **app** for crash / analytics SDKs. The CLI collects nothing |
| `maui-dev benchmark` | Shell to `maui-perf` (`Plugin.Maui.Performance.Cli`). Android / iOS simulator only |
| `maui-dev clean` | Delete `bin` / `obj` (optional NuGet HTTP cache and workload temp behind flags) |
| `maui-dev package` | Validate pack metadata (`--validate`, default). `--pack` runs `dotnet pack` locally and never pushes |

Global options: `--path`, `--format human|json|sarif`, `--ci` (JSON + warn-as-error), `--fix`, `--dry-run`, `--warn-as-error`, `--timeout`, `--no-update-check`.

On an interactive terminal the CLI asks every 4 hours whether to update from nuget.org (`[y/N]`, default no). Cache: `~/.nuvyntra/cli-updates.json`. Skip with `--no-update-check`, `NUVYNTRA_NO_UPDATE_CHECK=1`, or any `--ci` / JSON / SARIF run. `--no-update-check` shipped in **1.2.2**; 1.2.1 treats it as an unknown option. Nuvyn therefore calls `maui-dev doctor --path <app>` without that flag. The CLI does not phone home.

```bash
maui-dev doctor --fix --dry-run
maui-dev permissions --fix --dry-run
maui-dev version --align --dry-run
maui-dev publish --validate --ci
maui-dev migrate
maui-dev telemetry
maui-dev benchmark
maui-dev analyze --ci
maui-dev package --validate
```

`--fix` only:

- `doctor`: deduplicates identical `MauiSplashScreen` / `MauiImage` / `MauiIcon` / `MauiFont` items; inserts `<UseMaui>true</UseMaui>` when the project already looks like MAUI
- `permissions`: deduplicates identical Android `UsesPermission` / manifest nodes
- `version --align` / `--bump`: writes `Version` / `PackageVersion` and `extension/vscode/package.json`

It never bumps min SDK, removes permissions, writes signing secrets, installs workloads, or publishes NuGet packages.

Exit codes: `0` pass/skip, `1` fail (or warning with `--warn-as-error` / `--ci`), `2` usage error.

## CI

```yaml
- script: maui-dev doctor --ci
- script: maui-dev analyze --ci
- script: maui-dev permissions --ci
- script: maui-dev platform --ci
- script: maui-dev publish --validate --ci
- script: maui-dev migrate --ci
- script: maui-dev package --validate --ci
```

Publishing `Plugin.Maui.MauiDev.Cli` is pipeline-only on this repository. nuget.org reserved the ID `MauiDev.Cli` (the gallery page 404s and uploads are rejected). nuget.org uses the Actions secret `NUGET_KEY_MAUIDEV_CLI`; that key must be allowed to push `Plugin.Maui.*`. Do not run `dotnet nuget push` from a local clone.
