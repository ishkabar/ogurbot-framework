namespace ogur.core.Core;


public interface IScheduler
{
    Task Delay(TimeSpan due, CancellationToken ct);
}

public sealed class DefaultScheduler : IScheduler
{
    public Task Delay(TimeSpan due, CancellationToken ct) => Task.Delay(due, ct);
}