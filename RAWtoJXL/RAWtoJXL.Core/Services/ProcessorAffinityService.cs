using System.Diagnostics;
using System.Numerics;
using System.Runtime.InteropServices;

namespace RAWtoJXL.Core.Services;

public static class ProcessorAffinityService
{
    private const ushort AllProcessorGroups = ushort.MaxValue;

    public static bool TryExpandToAllLogicalProcessors()
    {
        if (!OperatingSystem.IsWindows())
        {
            return false;
        }

        try
        {
            int processorCount = checked((int)GetActiveProcessorCount(AllProcessorGroups));
            ulong affinityMask = BuildAffinityMask(processorCount);
            if (affinityMask == 0)
            {
                return false;
            }

            using var process = Process.GetCurrentProcess();
            ulong currentMask = unchecked((ulong)process.ProcessorAffinity.ToInt64());
            if (currentMask == affinityMask)
            {
                return true;
            }

            process.ProcessorAffinity = new IntPtr(unchecked((long)affinityMask));
            return true;
        }
        catch (Exception)
        {
            return false;
        }
    }

    internal static ulong BuildAffinityMask(int processorCount)
    {
        if (processorCount <= 0 || processorCount > 64)
        {
            return 0;
        }

        return processorCount == 64
            ? ulong.MaxValue
            : (1UL << processorCount) - 1;
    }

    internal static int GetCurrentAffinityProcessorCount()
    {
        if (!OperatingSystem.IsWindows())
        {
            return 0;
        }

        try
        {
            using var process = Process.GetCurrentProcess();
            return BitOperations.PopCount(unchecked((ulong)process.ProcessorAffinity.ToInt64()));
        }
        catch (Exception)
        {
            return 0;
        }
    }

    [DllImport("kernel32.dll", SetLastError = true)]
    private static extern uint GetActiveProcessorCount(ushort groupNumber);
}
