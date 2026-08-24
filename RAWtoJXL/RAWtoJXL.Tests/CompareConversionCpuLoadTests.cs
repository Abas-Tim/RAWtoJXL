using System.Diagnostics;
using Microsoft.Extensions.DependencyInjection;
using RAWtoJXL.Core;
using RAWtoJXL.Core.Interfaces;
using RAWtoJXL.Core.Models;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

[Collection("Conversion")]
[Trait("category", "diagnostic")]
public class CompareConversionCpuLoadTests
{
    private static readonly string[] ChildProcessNames = { "cjxl", "djxl", "rawtherapee-cli" };
    private readonly ITestOutputHelper _output;

    public CompareConversionCpuLoadTests(ITestOutputHelper output)
    {
        _output = output;
    }

    [Fact]
    public async Task InitialComparePipelines_ReportCpuLoadAndStageOverlap()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        string directory = Path.Combine(Path.GetTempPath(), $"RAWtoJXL_CompareCpu_{Guid.NewGuid():N}");
        Directory.CreateDirectory(directory);
        string inputPath = Path.Combine(directory, "source.dng");
        File.Copy(TestAssetGenerator.AssetPath, inputPath);

        using var process = Process.GetCurrentProcess();
        IntPtr originalAffinity = process.ProcessorAffinity;
        ProcessorAffinityService.TryExpandToAllLogicalProcessors();

        try
        {
            using var provider = new ServiceCollection()
                .AddCoreServices()
                .BuildServiceProvider();
            var conversionService = provider.GetRequiredService<ICompareConversionService>();
            using var cancellation = CancellationTokenSource.CreateLinkedTokenSource(TestContext.Current.CancellationToken);
            var token = cancellation.Token;
            int threadsPerVariant = CompareDefaults.GetJobThreads(2);

            var stopwatch = Stopwatch.StartNew();
            var originalTask = MeasureStage(
                "original-display",
                stopwatch,
                () => conversionService.EnsureDisplayPngsAsync(inputPath, null, 95, 9, token, null));
            var jxlTask = MeasureStage(
                "jxl-display",
                stopwatch,
                () => conversionService.EnsureDisplayPngsAsync(inputPath, OutputFormat.Jxl, 95, 9, token, threadsPerVariant));
            var avifTask = MeasureStage(
                "avif-display",
                stopwatch,
                () => conversionService.EnsureDisplayPngsAsync(inputPath, OutputFormat.Avif, 95, 9, token, threadsPerVariant));

            var displayTasks = Task.WhenAll(originalTask, jxlTask, avifTask);
            CpuLoadReport cpuReport = await SampleCpuAsync(
                displayTasks,
                stopwatch,
                Math.Max(1, CompareDefaults.JxlThreads),
                token);
            await displayTasks;

            StageMeasurement jxl = GetStage("jxl-display");
            StageMeasurement avif = GetStage("avif-display");
            TimeSpan displayOverlap = GetOverlap(jxl, avif);

            var targetJxlTask = MeasureStage(
                "jxl-target",
                stopwatch,
                () => conversionService.EnsureTargetFileAsync(inputPath, OutputFormat.Jxl, 94, 8, token, threadsPerVariant));
            var targetAvifTask = MeasureStage(
                "avif-target",
                stopwatch,
                () => conversionService.EnsureTargetFileAsync(inputPath, OutputFormat.Avif, 94, 8, token, threadsPerVariant));
            var targetTasks = Task.WhenAll(targetJxlTask, targetAvifTask);
            CpuLoadReport targetCpuReport = await SampleCpuAsync(
                targetTasks,
                stopwatch,
                Math.Max(1, CompareDefaults.JxlThreads),
                token);
            await targetTasks;

            StageMeasurement targetJxl = GetStage("jxl-target");
            StageMeasurement targetAvif = GetStage("avif-target");
            TimeSpan targetOverlap = GetOverlap(targetJxl, targetAvif);
            string childProcessReport = string.Join(
                ", ",
                cpuReport.ChildProcesses.Select(child =>
                    $"{child.Name}={child.Start.TotalSeconds:F2}-{child.End.TotalSeconds:F2}s"));
            string targetChildProcessReport = string.Join(
                ", ",
                targetCpuReport.ChildProcesses.Select(child =>
                    $"{child.Name}={child.Start.TotalSeconds:F2}-{child.End.TotalSeconds:F2}s"));

            process.Refresh();
            _output.WriteLine(
                $"Compare CPU diagnostic: affinity=0x{process.ProcessorAffinity.ToInt64():X}, " +
                $"logical={CompareDefaults.JxlThreads}, variantThreads={threadsPerVariant}, " +
                $"initial={cpuReport.Elapsed.TotalSeconds:F2}s, " +
                $"cpu={cpuReport.AveragePercent:F1}% avg/{cpuReport.PeakPercent:F1}% peak, " +
                $"samples={cpuReport.SampleCount}, childProcesses={cpuReport.ChildProcessCount} [{childProcessReport}], " +
                $"jxl={jxl.Start.TotalSeconds:F2}-{jxl.End.TotalSeconds:F2}s, " +
                $"avif={avif.Start.TotalSeconds:F2}-{avif.End.TotalSeconds:F2}s, " +
                $"jxlAvifDisplayOverlap={displayOverlap.TotalSeconds:F2}s, " +
                $"targets={targetCpuReport.Elapsed.TotalSeconds:F2}s, " +
                $"targetCpu={targetCpuReport.AveragePercent:F1}% avg/{targetCpuReport.PeakPercent:F1}% peak, " +
                $"targetProcesses={targetCpuReport.ChildProcessCount} [{targetChildProcessReport}], " +
                $"jxlTarget={targetJxl.Start.TotalSeconds:F2}-{targetJxl.End.TotalSeconds:F2}s, " +
                $"avifTarget={targetAvif.Start.TotalSeconds:F2}-{targetAvif.End.TotalSeconds:F2}s, " +
                $"jxlAvifTargetOverlap={targetOverlap.TotalSeconds:F2}s");

            Assert.True(File.Exists(jxlTask.Result.PreviewPath));
            Assert.True(File.Exists(avifTask.Result.PreviewPath));
            Assert.True(File.Exists(targetJxlTask.Result));
            Assert.True(File.Exists(targetAvifTask.Result));
            Assert.True(targetOverlap > TimeSpan.Zero, $"JXL and AVIF target stages did not overlap: {targetJxl} / {targetAvif}");
        }
        finally
        {
            process.ProcessorAffinity = originalAffinity;
            TryDeleteDirectory(directory);
        }

        StageMeasurement GetStage(string name)
        {
            lock (Stages)
            {
                return Stages.Single(stage => stage.Name == name);
            }
        }
    }

    private readonly List<StageMeasurement> Stages = new();

    private async Task<T> MeasureStage<T>(string name, Stopwatch stopwatch, Func<Task<T>> operation)
    {
        var measurement = new StageMeasurement(name, stopwatch.Elapsed, TimeSpan.Zero);
        lock (Stages)
        {
            Stages.Add(measurement);
        }

        try
        {
            return await operation().ConfigureAwait(false);
        }
        finally
        {
            lock (Stages)
            {
                int index = Stages.FindIndex(stage => stage.Name == name);
                Stages[index] = measurement with { End = stopwatch.Elapsed };
            }
        }
    }

    private static async Task<CpuLoadReport> SampleCpuAsync(
        Task operation,
        Stopwatch stopwatch,
        int processorCount,
        CancellationToken cancellationToken)
    {
        var ignoredChildIds = GetChildProcesses().Select(process => process.Id).ToHashSet();
        var knownChildProcesses = new Dictionary<int, ChildProcessMeasurement>();
        CpuSnapshot previous = CaptureCpu(ignoredChildIds, knownChildProcesses, stopwatch);
        TimeSpan totalCpu = TimeSpan.Zero;
        TimeSpan elapsed = TimeSpan.Zero;
        double peakPercent = 0;
        int sampleCount = 0;

        while (!operation.IsCompleted)
        {
            await Task.Delay(100, cancellationToken).ConfigureAwait(false);
            CpuSnapshot current = CaptureCpu(ignoredChildIds, knownChildProcesses, stopwatch);
            TimeSpan wallDelta = current.Timestamp - previous.Timestamp;
            TimeSpan cpuDelta = current.TotalCpu - previous.TotalCpu;
            if (wallDelta > TimeSpan.Zero && cpuDelta >= TimeSpan.Zero)
            {
                double percent = cpuDelta.TotalSeconds / wallDelta.TotalSeconds / processorCount * 100;
                totalCpu += cpuDelta;
                elapsed += wallDelta;
                peakPercent = Math.Max(peakPercent, percent);
                sampleCount++;
            }

            previous = current;
        }

        CpuSnapshot final = CaptureCpu(ignoredChildIds, knownChildProcesses, stopwatch);
        TimeSpan finalWallDelta = final.Timestamp - previous.Timestamp;
        TimeSpan finalCpuDelta = final.TotalCpu - previous.TotalCpu;
        if (finalWallDelta > TimeSpan.Zero && finalCpuDelta >= TimeSpan.Zero)
        {
            double percent = finalCpuDelta.TotalSeconds / finalWallDelta.TotalSeconds / processorCount * 100;
            totalCpu += finalCpuDelta;
            elapsed += finalWallDelta;
            peakPercent = Math.Max(peakPercent, percent);
            sampleCount++;
        }

        elapsed = elapsed > TimeSpan.Zero ? elapsed : stopwatch.Elapsed;
        double averagePercent = totalCpu.TotalSeconds / Math.Max(elapsed.TotalSeconds, 0.001) / processorCount * 100;
        return new CpuLoadReport(
            elapsed,
            averagePercent,
            peakPercent,
            sampleCount,
            knownChildProcesses.Count,
            knownChildProcesses.Values.ToArray());
    }

    private static CpuSnapshot CaptureCpu(
        IReadOnlySet<int> ignoredChildIds,
        IDictionary<int, ChildProcessMeasurement> knownChildProcesses,
        Stopwatch stopwatch)
    {
        using var current = Process.GetCurrentProcess();
        foreach (Process child in GetChildProcesses())
        {
            using (child)
            {
                if (ignoredChildIds.Contains(child.Id))
                {
                    continue;
                }

                try
                {
                    TimeSpan now = stopwatch.Elapsed;
                    if (!knownChildProcesses.TryGetValue(child.Id, out var measurement))
                    {
                        measurement = new ChildProcessMeasurement(child.ProcessName, now, now, TimeSpan.Zero);
                    }

                    knownChildProcesses[child.Id] = measurement with
                    {
                        End = now,
                        CpuTime = child.TotalProcessorTime
                    };
                }
                catch
                {
                }
            }
        }

        TimeSpan childCpu = knownChildProcesses.Values.Aggregate(
            TimeSpan.Zero,
            (total, measurement) => total + measurement.CpuTime);
        return new CpuSnapshot(
            GetTimestamp(),
            current.TotalProcessorTime + childCpu);
    }

    private static TimeSpan GetTimestamp()
    {
        return TimeSpan.FromSeconds((double)Stopwatch.GetTimestamp() / Stopwatch.Frequency);
    }

    private static Process[] GetChildProcesses()
    {
        return ChildProcessNames
            .SelectMany(Process.GetProcessesByName)
            .GroupBy(process => process.Id)
            .Select(group => group.First())
            .ToArray();
    }

    private static TimeSpan GetOverlap(StageMeasurement first, StageMeasurement second)
    {
        TimeSpan start = first.Start > second.Start ? first.Start : second.Start;
        TimeSpan end = first.End < second.End ? first.End : second.End;
        return end > start ? end - start : TimeSpan.Zero;
    }

    private static void TryDeleteDirectory(string path)
    {
        try
        {
            if (Directory.Exists(path))
            {
                Directory.Delete(path, true);
            }
        }
        catch
        {
        }
    }

    private sealed record StageMeasurement(string Name, TimeSpan Start, TimeSpan End);

    private sealed record CpuSnapshot(TimeSpan Timestamp, TimeSpan TotalCpu);

    private sealed record ChildProcessMeasurement(
        string Name,
        TimeSpan Start,
        TimeSpan End,
        TimeSpan CpuTime);

    private sealed record CpuLoadReport(
        TimeSpan Elapsed,
        double AveragePercent,
        double PeakPercent,
        int SampleCount,
        int ChildProcessCount,
        IReadOnlyList<ChildProcessMeasurement> ChildProcesses);
}
