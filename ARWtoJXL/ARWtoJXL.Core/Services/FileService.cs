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

        public string CombinePaths(string path1, string path2)
        {
            return Path.Combine(path1, path2);
        }

        public string GetTempFileName()
        {
            return Path.Combine(Path.GetTempPath(), Guid.NewGuid() + ".png");
        }
    }
}
