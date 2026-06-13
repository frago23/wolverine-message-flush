using Microsoft.Extensions.Logging;
using Wolverine.Attributes;

namespace ReproApp;

// The slow downstream handler. Because the "downstream" queue is Sequential
// (MaxDegreeOfParallelism = 1), this 2s-per-message work keeps the listener's
// bounded in-memory channel saturated during the trigger handler's burst, which
// is what causes the post-commit wake-up signals to be dropped.
public class DownstreamHandler(ILogger<DownstreamHandler> logger)
{
    [WolverineHandler]
    public async Task Handle(DownstreamMessage message, CancellationToken ct)
    {
        logger.LogInformation(
            "Processing DownstreamMessage Index={Index} Correlation={CorrelationId}",
            message.Index, message.CorrelationId);

        // Simulate slow work. 2 seconds per message is plenty to keep the
        // Sequential channel saturated during the burst.
        await Task.Delay(TimeSpan.FromSeconds(2), ct);
    }
}
