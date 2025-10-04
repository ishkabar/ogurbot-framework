using System;
using System.Threading;
using System.Threading.Tasks;

namespace Ogur.Core.Scheduler;

/// <summary>
/// Schedules lightweight recurring or delayed callbacks on thread pool.
/// </summary>
public interface IScheduler
{
    /// <summary>
    /// Schedules a recurring action that runs with the specified period until cancelled.
    /// </summary>
    /// <param name="key">Unique key to identify the scheduled job.</param>
    /// <param name="period">Execution period.</param>
    /// <param name="action">Callback to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task representing the scheduled loop; completes when cancelled.</returns>
    Task ScheduleRecurringAsync(string key, TimeSpan period, Func<CancellationToken, Task> action, CancellationToken ct);

    /// <summary>
    /// Schedules a one-shot action executed after a given delay.
    /// </summary>
    /// <param name="delay">Time to wait before execution.</param>
    /// <param name="action">Callback to execute.</param>
    /// <param name="ct">Cancellation token.</param>
    /// <returns>A task that completes after the action finishes or is cancelled.</returns>
    Task ScheduleOnceAsync(TimeSpan delay, Func<CancellationToken, Task> action, CancellationToken ct);

    /// <summary>
    /// Attempts to cancel a previously scheduled recurring job by key.
    /// </summary>
    /// <param name="key">Job key.</param>
    void Cancel(string key);
}