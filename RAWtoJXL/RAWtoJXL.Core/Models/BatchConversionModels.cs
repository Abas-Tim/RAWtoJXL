using System;
using System.Collections.Generic;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Settings;

namespace RAWtoJXL.Core.Models;

public enum BatchConversionStatus
{
    Converted,
    Skipped,
    Failed,
    Cancelled
}

public sealed record BatchConversionInput(
    string Id,
    string InputPath,
    int Quality);

/// <summary>
/// Immutable, per-file conversion settings captured before a batch starts.
/// OutputPath is reserved by BatchConversionPlanner and must not be recalculated
/// from mutable UI/settings state while the batch is running.
/// </summary>
public sealed record BatchConversionRequest(
    string Id,
    string InputPath,
    string? OutputPath,
    int Quality,
    OutputFormat OutputFormat,
    bool SkipMetadata,
    int? Effort,
    int? Threads,
    ConflictResolution Conflict,
    BatchConversionStatus? InitialStatus = null,
    string? InitialError = null);

public sealed record BatchConversionFileResult(
    string Id,
    string InputPath,
    string? OutputPath,
    BatchConversionStatus Status,
    string? Error,
    long InputBytes,
    long OutputBytes);

public sealed record BatchConversionProgress(
    string BatchId,
    string FileId,
    int CompletedCount,
    int TotalCount,
    double FileFraction,
    double OverallFraction,
    int ActiveJobs,
    int PeakActiveJobs);

public sealed record BatchConversionBatchResult(
    string BatchId,
    int Total,
    int Converted,
    int Skipped,
    int Failed,
    bool Cancelled,
    int PeakActiveJobs,
    IReadOnlyList<BatchConversionFileResult> Files);
