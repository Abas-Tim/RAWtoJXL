using System.Threading.Tasks;

namespace ARWtoJXL.Core.Interfaces
{
    public interface IPngCache
    {
        string? GetCachedPng(string inputPath);
        void StorePng(string inputPath, string pngPath);
        void EvictIfNeeded(long newFileSize);
    }
}
