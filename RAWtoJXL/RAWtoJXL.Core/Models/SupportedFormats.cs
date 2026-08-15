using System.Linq;

namespace RAWtoJXL.Core.Models
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

        public static readonly string[] RasterInputExtensions = [".jpg", ".jpeg", ".jxl", ".avif"];

        public static readonly string[] AllInputExtensions = RawExtensions.Concat(RasterInputExtensions).ToArray();

        public static readonly string[] OutputExtensions = [".jxl", ".jpg", ".avif"];

        public static bool IsRawFile(string extension)
        {
            return Array.Exists(RawExtensions, e => e.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsRasterInput(string extension)
        {
            return Array.Exists(RasterInputExtensions, e => e.Equals(extension, StringComparison.OrdinalIgnoreCase));
        }

        public static bool IsSupportedInput(string extension)
        {
            return IsRawFile(extension) || IsRasterInput(extension);
        }

        public static bool IsJxlFile(string extension)
        {
            return extension.Equals(".jxl", StringComparison.OrdinalIgnoreCase);
        }

        public static string ToFileFilter(string title, string[] extensions)
        {
            return $"{title}|{string.Join(";", extensions.Select(e => $"*{e}"))}";
        }
    }
}
