using System.CommandLine;
using MauiDev;

namespace MauiDev.Cli;

public static class CliHost
{
    public const string Usage =
        """
        maui-dev — developer productivity toolkit for .NET MAUI

          maui-dev doctor     Diagnose SDK, workloads, and project configuration
          maui-dev analyze    Heuristic source and project analysis
          maui-dev resources  Duplicate / missing / unused MAUI resources
          maui-dev clean      Delete bin and obj folders
          maui-dev package    Validate (default) or locally pack NuGet projects

        Options
          --path <dir>              Project or solution directory
          --format human|json|sarif Output format
          --ci                      json + no color + --warn-as-error
          --fix                     Apply allow-listed doctor fixes
          --dry-run                 Print fixes or deletions without writing
          --warn-as-error           Treat warnings as exit code 1
          --timeout <seconds>       Process timeout (default 30)

        clean extras
          --nuget-http-cache        Delete the NuGet HTTP cache (requires --yes)
          --workload-temp           Delete ~/.dotnet/workload-temp (requires --yes)
          --nuget-global            Require --force to delete ~/.nuget/packages
          --yes                     Confirm destructive cache deletes
          --force                   Required with --nuget-global

        package extras
          --validate                Default. Check versions, README, icon, license
          --pack                    Run dotnet pack locally. Never pushes.

        Examples
          maui-dev doctor
          maui-dev doctor --fix --dry-run
          maui-dev analyze --ci
          maui-dev package --validate

        Docs: https://github.com/nuvyntralabs/MauiDev
        """;

    public static Task<int> RunAsync(IReadOnlyList<string> args, TextWriter stdout, TextWriter stderr) =>
        RunAsync(args, stdout, stderr, root => CheckContext.CreateDefault(root));

    public static async Task<int> RunAsync(
        IReadOnlyList<string> args,
        TextWriter stdout,
        TextWriter stderr,
        Func<string, CheckContext> contextFactory)
    {
        if (args.Count == 0 || IsHelp(args[0]))
        {
            stdout.WriteLine(Usage);
            return ExitCodes.Success;
        }

        var root = BuildRoot(stdout, stderr, contextFactory);
        var parse = root.Parse([.. args]);
        if (parse.Errors.Count > 0 && !LooksLikeKnownCommand(args[0]))
        {
            stderr.WriteLine($"Unknown command '{args[0]}'.");
            stderr.WriteLine();
            stderr.WriteLine(Usage);
            return ExitCodes.Usage;
        }

        var configuration = new InvocationConfiguration
        {
            Output = stdout,
            Error = stderr
        };
        return await parse.InvokeAsync(configuration).ConfigureAwait(false);
    }

    static RootCommand BuildRoot(TextWriter stdout, TextWriter stderr, Func<string, CheckContext> contextFactory)
    {
        var pathOption = new Option<string?>("--path") { Description = "Project or solution directory" };
        var formatOption = new Option<string>("--format")
        {
            Description = "human | json | sarif",
            DefaultValueFactory = _ => "human"
        };
        var ciOption = new Option<bool>("--ci") { Description = "json + warn-as-error" };
        var fixOption = new Option<bool>("--fix") { Description = "Apply allow-listed fixes" };
        var dryRunOption = new Option<bool>("--dry-run") { Description = "Print changes without writing" };
        var warnOption = new Option<bool>("--warn-as-error") { Description = "Treat warnings as errors" };
        var timeoutOption = new Option<int>("--timeout")
        {
            Description = "Process timeout in seconds",
            DefaultValueFactory = _ => 30
        };
        var nugetHttp = new Option<bool>("--nuget-http-cache");
        var workloadTemp = new Option<bool>("--workload-temp");
        var nugetGlobal = new Option<bool>("--nuget-global");
        var yesOption = new Option<bool>("--yes");
        var forceOption = new Option<bool>("--force");
        var validateOption = new Option<bool>("--validate") { Description = "Validate pack metadata (default)" };
        var packOption = new Option<bool>("--pack") { Description = "Run dotnet pack locally" };

        var shared = new Option[] { pathOption, formatOption, ciOption, fixOption, dryRunOption, warnOption, timeoutOption };

        var doctor = new Command("doctor", "Diagnose MAUI environment and project configuration");
        Add(doctor, shared);
        doctor.SetAction(async (parse, token) =>
        {
            var options = Read(parse, pathOption, formatOption, ciOption, fixOption, dryRunOption, warnOption, timeoutOption);
            var context = CreateContext(options, contextFactory);
            var report = await new DoctorEngine().RunAsync(context, token).ConfigureAwait(false);
            return Write(report, options, stdout);
        });

        var analyze = new Command("analyze", "Heuristic source analysis");
        Add(analyze, shared);
        analyze.SetAction(parse =>
        {
            var options = Read(parse, pathOption, formatOption, ciOption, fixOption, dryRunOption, warnOption, timeoutOption);
            var context = CreateContext(options, contextFactory);
            return Write(new AnalyzeEngine().Run(context), options, stdout);
        });

        var resources = new Command("resources", "Duplicate, missing, and unused MAUI resources");
        Add(resources, shared);
        resources.SetAction(parse =>
        {
            var options = Read(parse, pathOption, formatOption, ciOption, fixOption, dryRunOption, warnOption, timeoutOption);
            var context = CreateContext(options, contextFactory);
            return Write(new ResourceEngine().Run(context), options, stdout);
        });

        var clean = new Command("clean", "Delete bin and obj folders");
        Add(clean, shared);
        clean.Options.Add(nugetHttp);
        clean.Options.Add(workloadTemp);
        clean.Options.Add(nugetGlobal);
        clean.Options.Add(yesOption);
        clean.Options.Add(forceOption);
        clean.SetAction(parse =>
        {
            var options = Read(parse, pathOption, formatOption, ciOption, fixOption, dryRunOption, warnOption, timeoutOption);
            var context = CreateContext(options, contextFactory);
            var request = new CleanRequest
            {
                NugetHttpCache = parse.GetValue(nugetHttp),
                WorkloadTemp = parse.GetValue(workloadTemp),
                NugetGlobal = parse.GetValue(nugetGlobal),
                Yes = parse.GetValue(yesOption),
                Force = parse.GetValue(forceOption)
            };
            return Write(new CleanEngine().Run(context, request), options, stdout);
        });

        var package = new Command("package", "Validate or locally pack NuGet projects");
        Add(package, shared);
        package.Options.Add(validateOption);
        package.Options.Add(packOption);
        package.SetAction(async (parse, token) =>
        {
            var options = Read(parse, pathOption, formatOption, ciOption, fixOption, dryRunOption, warnOption, timeoutOption);
            var context = CreateContext(options, contextFactory);
            var validator = new PackageValidator();
            if (parse.GetValue(packOption))
            {
                var (report, exit) = await validator.PackAsync(context, token).ConfigureAwait(false);
                ReporterFactory.Create(options.Format).Write(report, stdout);
                return exit;
            }

            return Write(validator.Validate(context), options, stdout);
        });

        var root = new RootCommand("MauiDev — developer productivity toolkit for .NET MAUI");
        root.Subcommands.Add(doctor);
        root.Subcommands.Add(analyze);
        root.Subcommands.Add(resources);
        root.Subcommands.Add(clean);
        root.Subcommands.Add(package);
        root.SetAction(_ =>
        {
            stdout.WriteLine(Usage);
            return ExitCodes.Success;
        });
        return root;
    }

    static void Add(Command command, IEnumerable<Option> options)
    {
        foreach (var option in options)
        {
            command.Options.Add(option);
        }
    }

    static GlobalOptions Read(
        ParseResult parse,
        Option<string?> path,
        Option<string> format,
        Option<bool> ci,
        Option<bool> fix,
        Option<bool> dryRun,
        Option<bool> warnAsError,
        Option<int> timeout)
    {
        var ciValue = parse.GetValue(ci);
        return new GlobalOptions(
            parse.GetValue(path),
            ReporterFactory.Parse(parse.GetValue(format), ciValue),
            ciValue,
            parse.GetValue(fix),
            parse.GetValue(dryRun),
            parse.GetValue(warnAsError) || ciValue,
            TimeSpan.FromSeconds(Math.Max(1, parse.GetValue(timeout))));
    }

    static CheckContext CreateContext(GlobalOptions options, Func<string, CheckContext> factory)
    {
        var files = new PhysicalFileSystem();
        var root = PathResolver.ResolveRoot(files, options.Path);
        var created = factory(root);
        return new CheckContext
        {
            RootPath = root,
            Process = created.Process,
            Files = created.Files,
            Host = created.Host,
            Ci = options.Ci,
            Fix = options.Fix,
            DryRun = options.DryRun,
            WarnAsError = options.WarnAsError,
            Timeout = options.Timeout,
            IgnoreIds = created.IgnoreIds.Count > 0 ? created.IgnoreIds : MauiDevIgnore.Load(created.Files, root)
        };
    }

    static int Write(DoctorReport report, GlobalOptions options, TextWriter stdout)
    {
        ReporterFactory.Create(options.Format).Write(report, stdout);
        return report.ExitCode(options.WarnAsError);
    }

    static bool IsHelp(string value) =>
        value is "-h" or "--help" or "-?" or "help";

    static bool LooksLikeKnownCommand(string value) =>
        value is "doctor" or "analyze" or "resources" or "clean" or "package";

    sealed record GlobalOptions(string? Path, ReportFormat Format, bool Ci, bool Fix, bool DryRun, bool WarnAsError, TimeSpan Timeout);
}
