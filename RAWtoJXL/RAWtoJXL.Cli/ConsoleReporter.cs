using System;
using System.Collections.Generic;
using System.IO;
using System.Text.Json;

namespace RAWtoJXL.Cli
{
    public sealed record PlanEntry(string Input, string? Output, string Status, string? Error = null);

    public sealed class ConsoleReporter
    {
        private static readonly JsonSerializerOptions JsonOptions = new()
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        private readonly TextWriter _stdout;
        private readonly TextWriter _stderr;
        private readonly bool _json;
        private readonly bool _quiet;
        private readonly bool _verbose;
        private readonly object _sync = new();
        private int _progressLength;

        public int Jobs { get; set; } = 1;

        public ConsoleReporter(TextWriter stdout, TextWriter stderr, bool json, bool quiet, bool verbose)
        {
            _stdout = stdout;
            _stderr = stderr;
            _json = json;
            _quiet = quiet;
            _verbose = verbose;
        }

        public void Progress(int index, int total, double fraction, string file)
        {
            if (_json || _quiet) return;
            var percent = Math.Clamp((int)(fraction * 100), 0, 100);
            var line = $"[{index}/{total}] {percent,3}% {Path.GetFileName(file)}";
            lock (_sync)
            {
                _stderr.Write("\r" + line.PadRight(_progressLength));
                _progressLength = line.Length;
            }
        }

        public void ClearProgressLine()
        {
            if (_json || _quiet || _progressLength == 0) return;
            lock (_sync)
            {
                _stderr.Write("\r" + new string(' ', _progressLength) + "\r");
                _progressLength = 0;
            }
        }

        public void FileCompleted(FileResult result, int completed, int total)
        {
            if (_json || _quiet || Jobs <= 1) return;
            lock (_sync)
            {
                switch (result.Status)
                {
                    case "converted":
                        _stderr.WriteLine($"[{completed}/{total}] {Path.GetFileName(result.Input)} -> {result.Output}");
                        break;
                    case "skipped":
                        _stderr.WriteLine($"[{completed}/{total}] {Path.GetFileName(result.Input)} skipped (output exists)");
                        break;
                    case "failed":
                        _stderr.WriteLine($"[{completed}/{total}] {Path.GetFileName(result.Input)} FAILED: {result.Error}");
                        break;
                    default:
                        _stderr.WriteLine($"[{completed}/{total}] {Path.GetFileName(result.Input)} {result.Status}");
                        break;
                }
            }
        }

        public void Verbose(FileResult result)
        {
            if (!_verbose || _json) return;
            var message = result.Status switch
            {
                "converted" => $"{result.Input} -> {result.Output} ({FormatSize(result.InputBytes)} -> {FormatSize(result.OutputBytes)})",
                "skipped" => $"{result.Input} skipped (output exists)",
                "failed" => $"{result.Input} FAILED: {result.Error}",
                _ => $"{result.Input} {result.Status}"
            };
            lock (_sync)
            {
                _stderr.WriteLine(message);
            }
        }

        public void Error(string message)
        {
            lock (_sync)
            {
                _stderr.WriteLine($"error: {message}");
            }
        }

        public void Warning(string message)
        {
            lock (_sync)
            {
                _stderr.WriteLine($"warning: {message}");
            }
        }

        public void Info(string message)
        {
            if (_json || _quiet) return;
            lock (_sync)
            {
                _stderr.WriteLine(message);
            }
        }

        public void Summary(BatchResult batch, bool dryRun, TimeSpan elapsed)
        {
            if (_json)
            {
                var document = new
                {
                    command = "convert",
                    dryRun,
                    batch.Total,
                    batch.Converted,
                    batch.Skipped,
                    batch.Failed,
                    batch.Cancelled,
                    elapsedSeconds = Math.Round(elapsed.TotalSeconds, 1),
                    files = batch.Files.Select(f => new
                    {
                        f.Input,
                        f.Output,
                        f.Status,
                        f.Error,
                        inputBytes = f.InputBytes,
                        outputBytes = f.OutputBytes
                    })
                };
                lock (_sync)
                {
                    _stdout.WriteLine(JsonSerializer.Serialize(document, JsonOptions));
                }
                return;
            }

            ClearProgressLine();
            lock (_sync)
            {
                if (batch.Cancelled)
                {
                    _stderr.WriteLine($"cancelled: converted {batch.Converted}, skipped {batch.Skipped}, failed {batch.Failed} of {batch.Total} ({FormatElapsed(elapsed)})");
                }
                else if (batch.Converted == batch.Total && batch.Failed == 0 && batch.Skipped == 0)
                {
                    _stderr.WriteLine($"done: converted {batch.Converted}/{batch.Total} ({FormatElapsed(elapsed)})");
                }
                else
                {
                    _stderr.WriteLine($"done: converted {batch.Converted}, skipped {batch.Skipped}, failed {batch.Failed} of {batch.Total} ({FormatElapsed(elapsed)})");
                }
            }
        }

        public void Plan(IReadOnlyList<PlanEntry> entries, string commandName)
        {
            if (_json)
            {
                var document = new
                {
                    command = commandName,
                    total = entries.Count,
                    planned = entries.Count(e => e.Status == "planned"),
                    skipped = entries.Count(e => e.Status == "skipped"),
                    files = entries.Select(e => new
                    {
                        e.Input,
                        e.Output,
                        e.Status,
                        e.Error
                    })
                };
                _stdout.WriteLine(JsonSerializer.Serialize(document, JsonOptions));
                return;
            }

            foreach (var entry in entries)
            {
                if (entry.Status == "skipped")
                {
                    _stdout.WriteLine($"{entry.Input} -> skipped (output exists)");
                }
                else
                {
                    _stdout.WriteLine($"{entry.Input} -> {entry.Output}");
                }
            }
            _stderr.WriteLine($"{entries.Count} file(s)");
        }

        public void Presets(IReadOnlyList<ConversionPresetInfo> presets)
        {
            if (_json)
            {
                var document = new
                {
                    command = "presets",
                    presets = presets.Select(p => new
                    {
                        p.Name,
                        p.Quality,
                        p.OutputFormat,
                        p.ConflictResolution,
                        p.UseSubfolder,
                        p.SubfolderName,
                        p.UseCustomOutputDirectory,
                        p.CustomOutputDirectory,
                        p.SkipMetadata,
                        p.CjxlEffort,
                        p.CjxlThreads
                    })
                };
                _stdout.WriteLine(JsonSerializer.Serialize(document, JsonOptions));
                return;
            }

            foreach (var preset in presets)
            {
                _stdout.WriteLine(preset.Name);
            }
        }

        private static string FormatSize(long bytes)
        {
            if (bytes <= 0) return "-";
            if (bytes < 1024) return $"{bytes} B";
            if (bytes < 1024 * 1024) return $"{bytes / 1024.0:F1} KB";
            return $"{bytes / (1024.0 * 1024.0):F1} MB";
        }

        private static string FormatElapsed(TimeSpan elapsed)
        {
            if (elapsed.TotalSeconds < 60) return $"{elapsed.TotalSeconds:F1}s";
            return $"{(int)elapsed.TotalMinutes}m {elapsed.Seconds}s";
        }
    }

    public sealed record ConversionPresetInfo(
        string Name,
        int Quality,
        string OutputFormat,
        string ConflictResolution,
        bool UseSubfolder,
        string SubfolderName,
        bool UseCustomOutputDirectory,
        string CustomOutputDirectory,
        bool SkipMetadata,
        int CjxlEffort,
        int CjxlThreads);
}
