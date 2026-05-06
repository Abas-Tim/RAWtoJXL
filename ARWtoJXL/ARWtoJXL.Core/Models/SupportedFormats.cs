using System.Linq;

namespace ARWtoJXL.Core.Models
{
    public static class SupportedFormats
    {
        public static readonly string[] RawExtensions =
        [
            ".arw",
            ".sr2",
            ".srf",
            ".cr2",
            ".cr3",
            ".crw",
            ".nef",
            ".nrw",
            ".raf",
            ".orf",
            ".rw2",
            ".dng"
        ];

        public static readonly string[] OutputExtensions = [".jxl", ".jpg", ".png"];

        public static bool IsRawFile(string extension)
        {
            return Array.Exists(RawExtensions, e => e.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static string ToFileFilter(string title, string[] extensions)
        {
            return $"{title}|{string.Join(";", extensions.Select(e => $"*{e}"))}";
        }
    }
}
