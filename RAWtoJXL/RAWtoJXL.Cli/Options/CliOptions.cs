using System;

namespace RAWtoJXL.Cli.Options
{
    public sealed class CliOptions
    {
        public string[] Paths { get; set; } = Array.Empty<string>();
        public string? Format { get; set; }
        public int Quality { get; set; } = -1;
        public string? Conflict { get; set; }
        public bool Recursive { get; set; }
        public string? OutputDirectory { get; set; }
        public bool NoSubfolder { get; set; }
        public string? Subfolder { get; set; }
        public string? Preset { get; set; }
        public string[] Extensions { get; set; } = Array.Empty<string>();
        public string[] Include { get; set; } = Array.Empty<string>();
        public string[] Exclude { get; set; } = Array.Empty<string>();
        public string? ModifiedAfter { get; set; }
        public string? ModifiedBefore { get; set; }
        public bool SkipMetadata { get; set; }
        public int Effort { get; set; } = -1;
        public int Threads { get; set; } = -1;
        public int Jobs { get; set; } = -1;
        public bool DryRun { get; set; }
        public bool Json { get; set; }
        public bool Quiet { get; set; }
        public bool Verbose { get; set; }
    }
}
