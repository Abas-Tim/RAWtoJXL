using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using RAWtoJXL.Core.Services;

namespace RAWtoJXL.Tests;

public class CompareJobBudgetTests
{
    [Fact]
    public void Acquire_ClampsToTotalThreads()
    {
        var budget = new CompareJobBudget(4);

        Assert.Equal(4, budget.Acquire(4));
        Assert.Equal(1, budget.Acquire(4));
        Assert.Equal(5, budget.GrantedThreads);
    }

    [Fact]
    public void Acquire_GrantsAtLeastOneWhenSaturated()
    {
        var budget = new CompareJobBudget(1);

        Assert.Equal(1, budget.Acquire(1));
        Assert.Equal(1, budget.Acquire(8));
    }

    [Fact]
    public void Release_RestoresAvailability()
    {
        var budget = new CompareJobBudget(6);

        int granted = budget.Acquire(4);
        budget.Release(granted);

        Assert.Equal(0, budget.GrantedThreads);
        Assert.Equal(3, budget.Acquire(3));
        Assert.Equal(3, budget.Acquire(3));
        Assert.Equal(6, budget.GrantedThreads);
    }

    [Fact]
    public async Task ConcurrentAcquireRelease_NeverDrainsBelowZero()
    {
        var budget = new CompareJobBudget(8);

        await Task.WhenAll(Enumerable.Range(0, 64).Select(async _ =>
        {
            for (int i = 0; i < 25; i++)
            {
                int grant = budget.Acquire(i % 5 + 1);
                Assert.True(grant >= 1);
                await Task.Yield();
                budget.Release(grant);
            }
        }));

        Assert.Equal(0, budget.GrantedThreads);
    }
}
