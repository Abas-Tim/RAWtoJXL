using System.IO;

namespace RAWtoJXL.Core.Interfaces
{
    public interface IFileService
    {
        void DeleteFile(string filePath);
        bool FileExists(string filePath);
        long GetFileSize(string filePath);
        string CombinePaths(string path1, string path2);
        string? SaveBytesToTemp(byte[] data, string extension);
    }
}
