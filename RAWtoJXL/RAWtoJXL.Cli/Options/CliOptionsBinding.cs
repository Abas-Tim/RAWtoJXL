using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.CommandLine;
using System.CommandLine.Parsing;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Cli.Options
{
    public sealed class CommandOptions
    {
        public Argument<string[]> Paths { get; } = new("paths")
        {
            Description = "Image files (RAW, JPEG, JXL, AVIF) or folders to process",
            Arity = ArgumentArity.OneOrMore
        };

        public Option<string> Format { get; } = new("--format", "-f")
        {
            Description = "Output format: jxl, jpg or avif"
        };

        public Option<int> Quality { get; } = new("--quality", "-q")
        {
            Description = "Output quality 0-100 (100 = lossless)",
            DefaultValueFactory = _ => -1
        };

        public Option<string> Conflict { get; } = new("--conflict")
        {
            Description = "Existing output handling: overwrite, skip or rename"
        };

        public Option<bool> Recursive { get; } = new("--recursive", "-r")
        {
            Description = "Scan folders recursively"
        };

        public Option<string> OutputDirectory { get; } = new("--output-dir", "-o")
        {
            Description = "Write all outputs to this directory"
        };

        public Option<bool> NoSubfolder { get; } = new("--no-subfolder")
        {
            Description = "Write next to the source file instead of into a subfolder"
        };

        public Option<string> Subfolder { get; } = new("--subfolder")
        {
            Description = "Subfolder name for outputs (default: jxl_output)"
        };

        public Option<string> Preset { get; } = new("--preset", "-p")
        {
            Description = "Named conversion preset from GUI settings"
        };

        public Option<string[]> Extensions { get; } = new("--ext")
        {
            Description = "Only these input extensions, e.g. --ext arw,cr3,jxl (put paths before this option)",
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.OneOrMore
        };

        public Option<string[]> Include { get; } = new("--include")
        {
            Description = "Only file names matching this wildcard, comma-separated or repeatable",
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.OneOrMore
        };

        public Option<string[]> Exclude { get; } = new("--exclude")
        {
            Description = "Skip file names matching this wildcard, comma-separated or repeatable",
            AllowMultipleArgumentsPerToken = true,
            Arity = ArgumentArity.OneOrMore
        };

        public Option<string> ModifiedAfter { get; } = new("--modified-after")
        {
            Description = "Only files modified after this ISO date (e.g. 2025-11-15)"
        };

        public Option<string> ModifiedBefore { get; } = new("--modified-before")
        {
            Description = "Only files modified before this ISO date"
        };

        public Option<bool> SkipMetadata { get; } = new("--skip-metadata")
        {
            Description = "Skip EXIF/XMP/ICC metadata embedding (faster)"
        };

        public Option<int> Effort { get; } = new("--effort")
        {
            Description = "cjxl encoding effort 1-9",
            DefaultValueFactory = _ => -1
        };

        public Option<int> Threads { get; } = new("--threads")
        {
            Description = "cjxl thread count (default: all cores)",
            DefaultValueFactory = _ => -1
        };

        public Option<int> Jobs { get; } = new("--jobs", "-j")
        {
            Description = $"Convert up to N files in parallel (default: {ParallelismPolicy.DefaultJobs}; a warning is emitted above the machine-specific stable limit)",
            DefaultValueFactory = _ => -1
        };

        public Option<bool> DryRun { get; } = new("--dry-run")
        {
            Description = "Resolve outputs and print the plan without converting"
        };

        public Option<bool> Json { get; } = new("--json")
        {
            Description = "Machine-readable JSON output on stdout"
        };

        public Option<bool> Quiet { get; } = new("--quiet")
        {
            Description = "Suppress progress output"
        };

        public Option<bool> Verbose { get; } = new("--verbose")
        {
            Description = "Per-file result lines"
        };
    }

    public static class CliOptionsBinding
    {
        public static CliOptions Bind(ParseResult parse, CommandOptions options)
        {
            var paths = parse.GetValue(options.Paths) ?? Array.Empty<string>();
            ValidatePaths(paths);

            var result = new CliOptions
            {
                Paths = paths,
                Format = NormalizeFormat(parse.GetValue(options.Format)),
                Quality = ValidateRange(parse.GetValue(options.Quality), 0, 100, "--quality"),
                Conflict = NormalizeConflict(parse.GetValue(options.Conflict)),
                Recursive = parse.GetValue(options.Recursive),
                OutputDirectory = parse.GetValue(options.OutputDirectory),
                NoSubfolder = parse.GetValue(options.NoSubfolder),
                Subfolder = parse.GetValue(options.Subfolder),
                Preset = parse.GetValue(options.Preset),
                Extensions = NormalizeExtensions(parse.GetValue(options.Extensions) ?? Array.Empty<string>()),
                Include = NormalizePatterns(parse.GetValue(options.Include) ?? Array.Empty<string>()),
                Exclude = NormalizePatterns(parse.GetValue(options.Exclude) ?? Array.Empty<string>()),
                ModifiedAfter = NormalizeDate(parse.GetValue(options.ModifiedAfter), "--modified-after"),
                ModifiedBefore = NormalizeDate(parse.GetValue(options.ModifiedBefore), "--modified-before"),
                SkipMetadata = parse.GetValue(options.SkipMetadata),
                Effort = ValidateRange(parse.GetValue(options.Effort), 1, 9, "--effort"),
                Threads = ValidateRange(parse.GetValue(options.Threads), 1, int.MaxValue, "--threads"),
                Jobs = ValidateRange(parse.GetValue(options.Jobs), 1, 1024, "--jobs"),
                DryRun = parse.GetValue(options.DryRun),
                Json = parse.GetValue(options.Json),
                Quiet = parse.GetValue(options.Quiet),
                Verbose = parse.GetValue(options.Verbose)
            };
            return result;
        }

        private static void ValidatePaths(string[] paths)
        {
            var missing = new System.Collections.Generic.List<string>();
            foreach (var path in paths)
            {
                if (path.StartsWith('-') && !File.Exists(path) && !Directory.Exists(path))
                {
                    throw new UsageException($"unrecognized option '{path}'.");
                }
                if (!File.Exists(path) && !Directory.Exists(path))
                {
                    missing.Add(path);
                }
            }

            if (missing.Count > 0)
            {
                throw new UsageException(
                    $"path does not exist: {string.Join(", ", missing.Select(p => $"'{p}'"))}.");
            }
        }

        private static string? NormalizeFormat(string? value)
        {
            if (value == null) return null;
            return value.ToLowerInvariant() switch
            {
                "jxl" => "Jxl",
                "jpg" or "jpeg" => "Jpeg",
                "avif" => "Avif",
                _ => throw new UsageException($"invalid --format '{value}'. Expected jxl, jpg or avif.")
            };
        }

        private static string? NormalizeConflict(string? value)
        {
            if (value == null) return null;
            return value.ToLowerInvariant() switch
            {
                "overwrite" => "Overwrite",
                "skip" => "Skip",
                "rename" or "append" or "appendnumber" => "AppendNumber",
                _ => throw new UsageException($"invalid --conflict '{value}'. Expected overwrite, skip or rename.")
            };
        }

        private static string[] NormalizePatterns(string[] values)
        {
            return values
                .SelectMany(v => v.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                .Select(p => p.Trim())
                .Where(p => p.Length > 0)
                .ToArray();
        }

        private static string[] NormalizeExtensions(string[] values)
        {
            var normalized = new System.Collections.Generic.List<string>();
            foreach (var raw in values)
            {
                foreach (var part in raw.Split(new[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries))
                {
                    var ext = part.Trim().TrimStart('.').ToLowerInvariant();
                    if (ext.Length == 0) continue;
                    if (!SupportedFormats.AllInputExtensions.Contains("." + ext))
                    {
                        throw new UsageException($"unsupported extension '{ext}'. Supported: {string.Join(", ", SupportedFormats.AllInputExtensions.Select(e => e.TrimStart('.')))}.");
                    }
                    normalized.Add("." + ext);
                }
            }
            return normalized.ToArray();
        }

        private static string? NormalizeDate(string? value, string optionName)
        {
            if (value == null) return null;
            if (DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed) ||
                DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out parsed))
            {
                return parsed.ToString("o", CultureInfo.InvariantCulture);
            }
            throw new UsageException($"invalid {optionName} date '{value}'. Expected ISO date like 2025-11-15.");
        }

        private static int ValidateRange(int value, int min, int max, string optionName)
        {
            if (value == -1) return -1;
            if (value < min || value > max)
            {
                throw new UsageException($"invalid {optionName} value {value}. Expected {min}-{max}.");
            }
            return value;
        }
    }
}
