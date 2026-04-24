using System;
using System.IO;
using ARWtoJXL.Core.Interfaces;

namespace ARWtoJXL.Core.Services
{
    public class FileService : IFileService
    {
        private readonly ILogger _logger;

        public FileService(ILogger logger)
        {
            _logger = logger;
        }

        public void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try
                {
                    File.Delete(filePath);
                }
                catch (Exception ex)
                {
                    _logger.Write($"[FileService] Failed to delete {filePath}: {ex.Message}");
                }
            }
        }

        public bool FileExists(string filePath)
        {
            return File.Exists(filePath);
        }

        public long GetFileSize(string filePath)
        {
            var info = new FileInfo(filePath);
            return info.Exists ? info.Length : 0L;
        }

        public string CombinePaths(string path1, string path2)
        {
            return Path.Combine(path1, path2);
        }

        public string GetTempFileName()
        {
            return Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        }

        public string? SaveBytesToTemp(byte[] data, string extension)
        {
            if (data == null || data.Length == 0)
                return null;

            var sanitizedExtension = Path.GetExtension(extension);
            if (string.IsNullOrEmpty(sanitizedExtension))
                sanitizedExtension = "." + extension.TrimStart('.');

            try
            {
                var tempFileName = Guid.NewGuid().ToString("N") + sanitizedExtension;
                var tempPath = Path.Combine(Path.GetTempPath(), tempFileName);
                File.WriteAllBytes(tempPath, data);
                return tempPath;
            }
            catch (Exception ex)
            {
                _logger.Write($"[FileService] Failed to save bytes to temp: {ex.Message}");
                return null;
            }
        }
    }
}
