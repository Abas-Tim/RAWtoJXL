using System;
using System.IO;
using ARWtoJXL.Core.Interfaces;

namespace ARWtoJXL.Core.Models
{
    public class MetadataProfiles : IDisposable
    {
        private readonly ILogger? _logger;

        public string? ExifPath { get; set; }
        public string? XmpPath { get; set; }
        public string? IccPath { get; set; }
        public string? IptcPath { get; set; }

        public bool HasAny => !string.IsNullOrEmpty(ExifPath) || !string.IsNullOrEmpty(XmpPath) || !string.IsNullOrEmpty(IccPath) || !string.IsNullOrEmpty(IptcPath);

        public MetadataProfiles(ILogger? logger = null)
        {
            _logger = logger;
        }

        public void Dispose()
        {
            TryDelete(ExifPath);
            TryDelete(XmpPath);
            TryDelete(IccPath);
            TryDelete(IptcPath);
        }

        private void TryDelete(string? path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try
                {
                    File.Delete(path);
                }
                catch (IOException ex)
                {
                    _logger?.Write($"[MetadataProfiles] Failed to delete temp file {path}: {ex.Message}");
                }
                catch (UnauthorizedAccessException ex)
                {
                    _logger?.Write($"[MetadataProfiles] Access denied deleting temp file {path}: {ex.Message}");
                }
            }
        }
    }
}
