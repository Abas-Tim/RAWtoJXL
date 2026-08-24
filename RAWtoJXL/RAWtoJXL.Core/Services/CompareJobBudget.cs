using System;
using System.Threading;

namespace RAWtoJXL.Core.Services;

public sealed class CompareJobBudget
{
    private readonly int _totalThreads;
    private int _grantedThreads;

    public CompareJobBudget(int totalThreads)
    {
        _totalThreads = Math.Max(1, totalThreads);
    }

    public int TotalThreads => _totalThreads;

    public int GrantedThreads => Volatile.Read(ref _grantedThreads);

    public int Acquire(int requested)
    {
        requested = Math.Max(1, requested);
        while (true)
        {
            int current = Volatile.Read(ref _grantedThreads);
            int available = Math.Max(0, _totalThreads - current);
            int grant = Math.Max(1, Math.Min(requested, available));
            if (Interlocked.CompareExchange(ref _grantedThreads, current + grant, current) == current)
            {
                return grant;
            }
        }
    }

    public void Release(int threads)
    {
        if (threads <= 0)
        {
            return;
        }

        while (true)
        {
            int current = Volatile.Read(ref _grantedThreads);
            int next = Math.Max(0, current - threads);
            if (Interlocked.CompareExchange(ref _grantedThreads, next, current) == current)
            {
                return;
            }
        }
    }
}
