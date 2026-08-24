using System;
using System.IO;

namespace RAWtoJXL.Core.Models;

public sealed class MasterRenderLease : IDisposable
{
    private readonly string? _ownedTempPath;
    private bool _completed;

    private MasterRenderLease(string pngPath, bool isPromotedMaster, string? ownedTempPath)
    {
        PngPath = pngPath;
        IsPromotedMaster = isPromotedMaster;
        _ownedTempPath = ownedTempPath;
    }

    public string PngPath { get; }

    public bool IsPromotedMaster { get; }

    internal static MasterRenderLease ForMaster(string masterPath) => new(masterPath, true, null);

    internal static MasterRenderLease ForTemp(string tempPath) => new(tempPath, false, tempPath);

    public void Complete()
    {
        if (_completed)
        {
            return;
        }

        _completed = true;
        if (_ownedTempPath == null)
        {
            return;
        }

        try
        {
            if (File.Exists(_ownedTempPath))
            {
                File.Delete(_ownedTempPath);
            }
        }
        catch
        {
        }
    }

    public void Dispose() => Complete();
}
