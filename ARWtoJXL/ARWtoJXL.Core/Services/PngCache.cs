using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading;
using ARWtoJXL.Core.Interfaces;

namespace ARWtoJXL.Core.Services
{
    public class PngCache : IPngCache
    {
        private readonly string _cacheDir;
        private readonly long _maxCacheSizeBytes;
        private readonly object _lock = new();
        private readonly Dictionary<string, string> _hashToPath = new();
        private long _totalCacheSize;

        public PngCache(ILogger logger)
        {
            _cacheDir = Path.Combine(Path.GetTempPath(), "ARWtoJXL", "png_cache");
            _maxCacheSizeBytes = 2L * 1024 * 1024 * 1024;
            Directory.CreateDirectory(_cacheDir);
            RebuildIndex();
            logger.Write($"[PngCache] Initialized at {_cacheDir}, size: {_totalCacheSize} bytes");
        }

        private void RebuildIndex()
        {
            try
            {
                foreach (var file in Directory.EnumerateFiles(_cacheDir, "*.png"))
                {
                    var hash = Path.GetFileNameWithoutExtension(file);
                    if (hash.Length == 64)
                    {
                        _hashToPath[hash] = file;
                        _totalCacheSize += new FileInfo(file).Length;
                    }
                }
            }
            catch (Exception)
            {
            }
        }

        public string? GetCachedPng(string inputPath)
        {
            lock (_lock)
            {
                var hash = ComputeHash(inputPath);
                if (_hashToPath.TryGetValue(hash, out var cachedPath) && File.Exists(cachedPath))
                {
                    File.SetLastAccessTime(cachedPath, DateTime.UtcNow);
                    return cachedPath;
                }
                return null;
            }
        }

        public void StorePng(string inputPath, string pngPath)
        {
            lock (_lock)
            {
                var hash = ComputeHash(inputPath);
                EvictIfNeededInternal();
                var destPath = Path.Combine(_cacheDir, hash + ".png");
                try
                {
                    File.Copy(pngPath, destPath, overwrite: true);
                    _hashToPath[hash] = destPath;
                    _totalCacheSize += new FileInfo(destPath).Length;
                }
                catch (Exception)
                {
                }
            }
        }

        public void EvictIfNeeded(long newFileSize)
        {
            lock (_lock)
            {
                if (_totalCacheSize + newFileSize > _maxCacheSizeBytes)
                {
                    EvictInternal(newFileSize);
                }
            }
        }

        private void EvictIfNeededInternal()
        {
            EvictInternal(0);
        }

        private void EvictInternal(long additionalSize)
        {
            var targetSize = _maxCacheSizeBytes / 2;
            while (_totalCacheSize + additionalSize > targetSize && _hashToPath.Any())
            {
                var oldest = _hashToPath.OrderBy(kvp => File.GetLastAccessTime(kvp.Value)).First();
                try
                {
                    var size = new FileInfo(oldest.Value).Length;
                    File.Delete(oldest.Value);
                    _totalCacheSize -= size;
                }
                catch (Exception)
                {
                }
                _hashToPath.Remove(oldest.Key);
            }
        }

        private static string ComputeHash(string inputPath)
        {
            var info = new FileInfo(inputPath);
            var content = $"{inputPath}|{info.LastWriteTimeUtc:O}|{info.Length}";
            using var sha256 = SHA256.Create();
            var bytes = System.Text.Encoding.UTF8.GetBytes(content);
            var hash = sha256.ComputeHash(bytes);
            return Convert.ToHexString(hash).ToLowerInvariant();
        }
    }
}
