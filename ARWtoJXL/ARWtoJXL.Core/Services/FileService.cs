using System;
using System.IO;

namespace ARWtoJXL.Core.Services
{
    public class FileService : Interfaces.IFileService
    {
        public void DeleteFile(string filePath)
        {
            if (File.Exists(filePath))
            {
                try { File.Delete(filePath); } catch { }
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
            catch
            {
                return null;
            }
        }
    }
}
