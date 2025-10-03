namespace ogur.core.Core;

public interface IScheduler
{
    Task Delay(TimeSpan due, CancellationToken ct);
}