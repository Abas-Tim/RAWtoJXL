using System.IO;

namespace ARWtoJXL.Core.Interfaces
{
    public interface IFileService
    {
        void DeleteFile(string filePath);
        bool FileExists(string filePath);
        long GetFileSize(string filePath);
        string CombinePaths(string path1, string path2);
        string GetTempFileName();
        string? SaveBytesToTemp(byte[] data, string extension);
    }
}
