namespace ogur.core.Core;

public sealed class DefaultScheduler : IScheduler
{
    public Task Delay(TimeSpan due, CancellationToken ct) => Task.Delay(due, ct);
}