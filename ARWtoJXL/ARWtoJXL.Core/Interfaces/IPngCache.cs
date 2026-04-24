using System;

namespace ARWtoJXL.Core.Interfaces
{
    public interface IPngCache : IDisposable
    {
        string? GetCachedPng(string inputPath);
        void StorePng(string inputPath, string pngPath);
        void EvictIfNeeded(long newFileSize);
    }
}
