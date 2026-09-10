using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using RAWtoJXL.Core.Models;

namespace RAWtoJXL.Core.Interfaces;

public interface IBatchConversionService
{
    Task<BatchConversionBatchResult> RunAsync(
        IReadOnlyList<BatchConversionRequest> requests,
        int jobs,
        Action<BatchConversionProgress>? progress = null,
        Func<BatchConversionFileResult, int, Task>? fileCompleted = null,
        CancellationToken cancellationToken = default);
}
