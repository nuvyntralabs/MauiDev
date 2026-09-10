# maui-dev

A [dotnet tool](https://learn.microsoft.com/dotnet/core/tools/global-tools) that diagnoses .NET MAUI environments and project configuration.

```bash
dotnet tool install -g MauiDev.Cli
maui-dev doctor
maui-dev analyze --ci
maui-dev resources
maui-dev clean
maui-dev package --validate
```

`--ci` prints JSON for GitHub Actions and the MauiDev VS Code / Cursor extension. `doctor --fix` only applies allow-listed edits (duplicate `MauiSplashScreen` / `MauiImage` items, missing `UseMaui`). It never bumps min SDK, removes permissions, writes signing secrets, or pushes NuGet packages.

Pair with `Plugin.Maui.Performance.Cli` (`maui-perf`) for traces and `Plugin.Maui.LeakAnalyser` for visual-tree leaks. MauiDev does not replace those packages.

Docs: https://github.com/nuvyntralabs/MauiDev
