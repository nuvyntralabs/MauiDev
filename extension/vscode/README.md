# MauiDev for VS Code and Cursor

Runs the `maui-dev` .NET global tool and maps JSON diagnostics into the Problems panel.

## Commands

- MauiDev: Doctor
- MauiDev: Analyze
- MauiDev: Resources
- MauiDev: Permissions
- MauiDev: Platform
- MauiDev: Signing
- MauiDev: Workload
- MauiDev: Version
- MauiDev: Dependencies
- MauiDev: Icons
- MauiDev: Validate Publish
- MauiDev: Migrate
- MauiDev: Telemetry scan
- MauiDev: Benchmark
- MauiDev: Clean
- MauiDev: Validate Package
- MauiDev: Show last report
- MauiDev: Install Plugin.Maui.MauiDev.Cli

If `maui-dev` is not on PATH (or `~/.dotnet/tools`), the extension offers to run `dotnet tool install -g Plugin.Maui.MauiDev.Cli --source https://api.nuget.org/v3/index.json`.

Settings: `mauiDev.toolPath`, `mauiDev.warnAsError`, `mauiDev.autoDoctorOnOpen` (off by default).
