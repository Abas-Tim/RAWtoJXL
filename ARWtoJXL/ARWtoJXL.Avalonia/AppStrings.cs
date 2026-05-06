using ARWtoJXL.Core.Models;

namespace ARWtoJXL.Avalonia
{
    public static class AppStrings
    {
        public const string SelectAll = "Select All";
        public const string DeselectAll = "Deselect All";
        public const string True = "True";
        public const string False = "False";
        public const string Ready = "Ready";
        public const string Converting = "Converting";
        public const string Converted = "Converted";
        public const string Failed = "Failed";
        public const string Pending = "Pending";
        public const string Cancelled = "Cancelled";
        public const string ConversionComplete = "Conversion complete.";
        public const string Cancelling = "Cancelling...";
        public const string FailedToOpenOutputFolder = "Failed to open output folder.";
        public const string OpenFileDialogTitle = "Open Image Files";
        public static string OpenFileDialogFilter =>
            $"{SupportedFormats.ToFileFilter("RAW Files", SupportedFormats.RawExtensions)}|{SupportedFormats.ToFileFilter("JPEG XL Files", new[] { ".jxl" })}|All Files|*.*";
        public const string ThumbnailFailedPrefix = "Thumbnail failed: ";
        public const string ProgressErrorPrefix = "Progress error: ";
        public const string FileLockedPrefix = "File locked: ";
        public const string ItemsRemoved = "Removed ";
        public const string ItemsSuffix = " item(s).";
        public const string ConvertingProgress = "Converting ";
        public const string OfSuffix = " of ";
        public const string SubfolderNameDefault = "jxl_output";
        public const string FileSkipped = "Skipped (file exists)";
        public const string FileSkippedByUser = "Skipped by user";
        public const string CacheCleared = "PNG cache cleared.";
        public const string CacheClearFailed = "Failed to clear PNG cache.";
        public const string CacheInfo = "PNG Cache: ";
    }
}
