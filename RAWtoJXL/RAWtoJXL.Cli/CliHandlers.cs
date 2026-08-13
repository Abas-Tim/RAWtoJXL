using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.CommandLine;
using System.CommandLine.Parsing;
using Microsoft.Extensions.DependencyInjection;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Services;
using RAWtoJXL.Core.Settings;
using RAWtoJXL.Cli.Options;

namespace RAWtoJXL.Cli
{
    internal static class CliHandlers
    {
        public static async Task<int> ConvertAsync(
            ParseResult parse,
            CommandOptions options,
            IServiceProvider services,
            TextWriter stdout,
            TextWriter stderr,
            CancellationToken cancellationToken)
        {
            var (cli, reporter) = BindAndCreateReporter(parse, options, stdout, stderr, out var exitCode);
            if (exitCode != null) return exitCode.Value;

            var settings = SettingsService.Load();
            var preset = ResolvePreset(cli.Preset, settings);
            if (cli.Preset != null && preset == null)
            {
                reporter.Error($"preset '{cli.Preset}' not found in settings");
                return ExitCodes.Usage;
            }

            var resolved = SettingsMerger.Merge(cli, settings, preset);
            var files = EnumerateAndFilter(cli, resolved);
            if (files.Count == 0)
            {
                ReportNoFiles(cli, reporter);
                return ExitCodes.NoFiles;
            }

            var requestedJobs = cli.Jobs > 0 ? cli.Jobs : ParallelismPolicy.DefaultJobs;
            if (ParallelismPolicy.IsAboveSafeMax(requestedJobs))
            {
                var extra = requestedJobs > ParallelismPolicy.HardCap
                    ? $" Values above {ParallelismPolicy.HardCap} are known to crash libjxl 0.11.2."
                    : string.Empty;
                reporter.Warning(
                    $"{requestedJobs} parallel jobs exceed the stable limit of {ParallelismPolicy.SafeMaxJobs} " +
                    $"for this machine ({Environment.ProcessorCount} logical processors, " +
                    $"{GC.GetGCMemoryInfo().TotalAvailableMemoryBytes / (1024L * 1024 * 1024)} GB RAM)." + extra);
            }

            if (cli.DryRun)
            {
                reporter.Plan(BuildPlan(files, resolved), "convert");
                return ExitCodes.Success;
            }

            if (!await ToolPreflight.VerifyAsync(services, stderr))
            {
                return ExitCodes.ToolMissing;
            }

            var imageService = services.GetRequiredService<IImageService>();
            var runner = new ConversionRunner(imageService);

            var jobs = Math.Min(requestedJobs, files.Count);
            reporter.Jobs = jobs;
            if (jobs > 1)
            {
                reporter.Info($"converting with {jobs} parallel jobs");
            }

            var stopwatch = System.Diagnostics.Stopwatch.StartNew();
            var batch = await runner.RunAsync(
                files,
                resolved,
                jobs,
                reporter.Progress,
                (result, completed) => reporter.FileCompleted(result, completed, files.Count),
                cancellationToken);
            stopwatch.Stop();

            foreach (var result in batch.Files)
            {
                reporter.Verbose(result);
            }
            reporter.Summary(batch, dryRun: false, stopwatch.Elapsed);

            if (batch.Cancelled) return ExitCodes.Cancelled;
            return batch.Failed > 0 ? ExitCodes.PartialFailure : ExitCodes.Success;
        }

        public static async Task<int> ListAsync(
            ParseResult parse,
            CommandOptions options,
            IServiceProvider services,
            TextWriter stdout,
            TextWriter stderr,
            CancellationToken cancellationToken)
        {
            var (cli, reporter) = BindAndCreateReporter(parse, options, stdout, stderr, out var exitCode);
            if (exitCode != null) return exitCode.Value;

            var settings = SettingsService.Load();
            var preset = ResolvePreset(cli.Preset, settings);
            if (cli.Preset != null && preset == null)
            {
                reporter.Error($"preset '{cli.Preset}' not found in settings");
                return ExitCodes.Usage;
            }

            var resolved = SettingsMerger.Merge(cli, settings, preset);
            var files = EnumerateAndFilter(cli, resolved);
            if (files.Count == 0)
            {
                ReportNoFiles(cli, reporter);
                return ExitCodes.NoFiles;
            }

            reporter.Plan(BuildPlan(files, resolved), "list");
            return ExitCodes.Success;
        }

        public static int PresetsAsync(
            ParseResult parse,
            Option<bool> jsonOption,
            TextWriter stdout,
            TextWriter stderr)
        {
            var json = parse.GetValue(jsonOption);
            var settings = SettingsService.Load();
            var presets = settings.Presets.Select(p => new ConversionPresetInfo(
                p.Name,
                p.Quality,
                p.OutputFormat.ToString(),
                p.ConflictResolution.ToString(),
                p.UseSubfolder,
                p.SubfolderName,
                p.UseCustomOutputDirectory,
                p.CustomOutputDirectory,
                p.SkipMetadata,
                p.CjxlEffort,
                p.CjxlThreads)).ToList();

            var reporter = new ConsoleReporter(stdout, stderr, json, quiet: false, verbose: false);
            reporter.Presets(presets);
            return ExitCodes.Success;
        }

        private static (CliOptions Cli, ConsoleReporter Reporter) BindAndCreateReporter(
            ParseResult parse,
            CommandOptions options,
            TextWriter stdout,
            TextWriter stderr,
            out int? exitCode)
        {
            CliOptions cli;
            try
            {
                cli = CliOptionsBinding.Bind(parse, options);
            }
            catch (UsageException ex)
            {
                stderr.WriteLine($"error: {ex.Message}");
                exitCode = ExitCodes.Usage;
                return (null!, null!);
            }

            var reporter = new ConsoleReporter(stdout, stderr, cli.Json, cli.Quiet, cli.Verbose);
            exitCode = null;
            return (cli, reporter);
        }

        private static ConversionPreset? ResolvePreset(string? presetName, AppSettings settings)
        {
            if (presetName == null) return null;
            return settings.Presets.FirstOrDefault(p =>
                string.Equals(p.Name, presetName, StringComparison.OrdinalIgnoreCase));
        }

        private static IReadOnlyList<string> EnumerateAndFilter(CliOptions cli, ResolvedOptions resolved)
        {
            var files = ImageFileEnumerator.Enumerate(cli.Paths, resolved.Recursive, resolved.Extensions);
            return FileFilter.Apply(files, resolved);
        }

        private static void ReportNoFiles(CliOptions cli, ConsoleReporter reporter)
        {
            if (cli.Json)
            {
                reporter.Plan(Array.Empty<PlanEntry>(), "convert");
                return;
            }
            reporter.Info("no files found matching the given paths and filters");
        }

        private static IReadOnlyList<PlanEntry> BuildPlan(IReadOnlyList<string> files, ResolvedOptions options)
        {
            var entries = new List<PlanEntry>(files.Count);
            foreach (var file in files)
            {
                var output = OutputPathResolver.Resolve(
                    file,
                    options.Format,
                    options.Conflict,
                    options.UseCustomOutputDirectory,
                    options.CustomOutputDirectory,
                    options.UseSubfolder,
                    options.SubfolderName,
                    createDirectory: false);
                entries.Add(output == null
                    ? new PlanEntry(file, null, "skipped")
                    : new PlanEntry(file, output, "planned"));
            }
            return entries;
        }
    }
}
