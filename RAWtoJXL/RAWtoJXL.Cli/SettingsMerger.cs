using System;
using System.Collections.Generic;
using System.Linq;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Settings;
using RAWtoJXL.Cli.Options;

namespace RAWtoJXL.Cli
{
    public sealed record ResolvedOptions(
        OutputFormat Format,
        int Quality,
        ConflictResolution Conflict,
        bool UseSubfolder,
        string SubfolderName,
        bool UseCustomOutputDirectory,
        string CustomOutputDirectory,
        bool SkipMetadata,
        int? Effort,
        int? Threads,
        bool Recursive,
        IReadOnlyList<string> Extensions,
        IReadOnlyList<string> Include,
        IReadOnlyList<string> Exclude,
        DateTime? ModifiedAfter,
        DateTime? ModifiedBefore);

    public static class SettingsMerger
    {
        public static ResolvedOptions Merge(CliOptions cli, AppSettings settings, ConversionPreset? preset)
        {
            var format = ParseFormat(cli.Format) ?? preset?.OutputFormat ?? settings.OutputFormat;
            var quality = cli.Quality >= 0 ? cli.Quality : (preset?.Quality ?? settings.QualityPreset);
            var conflict = ParseConflict(cli.Conflict) ?? preset?.ConflictResolution ?? settings.ConflictResolution;

            var useCustomOutputDirectory = cli.OutputDirectory != null
                || (preset?.UseCustomOutputDirectory ?? settings.UseCustomOutputDirectory);
            var customOutputDirectory = cli.OutputDirectory
                ?? (useCustomOutputDirectory ? (preset?.CustomOutputDirectory ?? settings.CustomOutputDirectory) : string.Empty);

            var useSubfolder = cli.NoSubfolder
                ? false
                : cli.Subfolder != null || (preset?.UseSubfolder ?? settings.UseSubfolder);
            var subfolderName = cli.Subfolder
                ?? (string.IsNullOrWhiteSpace(preset?.SubfolderName) ? settings.SubfolderName : preset!.SubfolderName);

            var skipMetadata = cli.SkipMetadata || (preset?.SkipMetadata ?? settings.SkipMetadata);
            var effort = cli.Effort >= 1 ? cli.Effort
                : (preset?.CjxlEffort >= 1 ? preset.CjxlEffort
                    : (settings.CjxlEffort >= 1 ? settings.CjxlEffort : (int?)null));
            var threads = cli.Threads >= 1 ? cli.Threads
                : (preset?.CjxlThreads > 0 ? preset.CjxlThreads
                    : (settings.CjxlThreads > 0 ? settings.CjxlThreads : (int?)null));

            var recursive = cli.Recursive || settings.SearchRecursive;
            var extensions = cli.Extensions.Length > 0 ? cli.Extensions : SupportedFormats.AllInputExtensions;
            var modifiedAfter = ParseDate(cli.ModifiedAfter);
            var modifiedBefore = ParseDate(cli.ModifiedBefore);

            return new ResolvedOptions(
                format,
                quality,
                conflict,
                useSubfolder,
                subfolderName,
                useCustomOutputDirectory,
                customOutputDirectory,
                skipMetadata,
                effort,
                threads,
                recursive,
                extensions,
                cli.Include,
                cli.Exclude,
                modifiedAfter,
                modifiedBefore);
        }

        internal static OutputFormat? ParseFormat(string? value)
        {
            if (value == null) return null;
            return Enum.TryParse<OutputFormat>(value, ignoreCase: true, out var format) ? format : null;
        }

        internal static ConflictResolution? ParseConflict(string? value)
        {
            if (value == null) return null;
            return Enum.TryParse<ConflictResolution>(value, ignoreCase: true, out var conflict) ? conflict : null;
        }

        internal static DateTime? ParseDate(string? value)
        {
            if (value == null) return null;
            if (DateTime.TryParse(value, System.Globalization.CultureInfo.InvariantCulture, System.Globalization.DateTimeStyles.None, out var date))
            {
                return DateTime.SpecifyKind(date, DateTimeKind.Local);
            }
            return null;
        }
    }
}
