namespace ARWtoJXL.Core.Models
{
    public class MetadataProfiles : IDisposable
    {
        public string? ExifPath { get; set; }
        public string? XmpPath { get; set; }
        public string? IccPath { get; set; }
        public string? IptcPath { get; set; }

        public bool HasAny => !string.IsNullOrEmpty(ExifPath) || !string.IsNullOrEmpty(XmpPath) || !string.IsNullOrEmpty(IccPath) || !string.IsNullOrEmpty(IptcPath);

        public void Dispose()
        {
            TryDelete(ExifPath);
            TryDelete(XmpPath);
            TryDelete(IccPath);
            TryDelete(IptcPath);
        }

        private static void TryDelete(string? path)
        {
            if (!string.IsNullOrEmpty(path) && File.Exists(path))
            {
                try { File.Delete(path); } catch { }
            }
        }
    }
}
