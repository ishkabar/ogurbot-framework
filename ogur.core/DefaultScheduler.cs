using System;
using System.Collections.Concurrent;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;

namespace Ogur.Core.Scheduler;

/// <summary>
/// Default <see cref="IScheduler"/> implementation using <see cref="PeriodicTimer"/> and cooperative cancellation.
/// </summary>
public sealed class DefaultScheduler : IScheduler
{
    private readonly ILogger<DefaultScheduler> _logger;
    private readonly ConcurrentDictionary<string, CancellationTokenSource> _recurring = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>
    /// Initializes a new instance of the <see cref="DefaultScheduler"/> class.
    /// </summary>
    /// <param name="logger">Logger instance.</param>
    public DefaultScheduler(ILogger<DefaultScheduler> logger)
    {
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ScheduleRecurringAsync(string key, TimeSpan period, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(key)) throw new ArgumentException("Key is required.", nameof(key));
        if (period <= TimeSpan.Zero) throw new ArgumentOutOfRangeException(nameof(period));

        var cts = CancellationTokenSource.CreateLinkedTokenSource(ct);
        if (!_recurring.TryAdd(key, cts))
        {
            Cancel(key);
            _recurring[key] = cts;
        }

        _ = RunRecurringAsync(key, period, action, cts.Token);
        await Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task ScheduleOnceAsync(TimeSpan delay, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        if (delay < TimeSpan.Zero) delay = TimeSpan.Zero;
        await Task.Delay(delay, ct);
        if (!ct.IsCancellationRequested)
        {
            await action(ct);
        }
    }

    /// <inheritdoc />
    public void Cancel(string key)
    {
        if (_recurring.TryRemove(key, out var cts))
        {
            cts.Cancel();
            cts.Dispose();
            _logger.LogInformation("Cancelled scheduled job {Key}.", key);
        }
    }

    private async Task RunRecurringAsync(string key, TimeSpan period, Func<CancellationToken, Task> action, CancellationToken ct)
    {
        try
        {
            var timer = new PeriodicTimer(period);
            using (timer)
            {
                while (await timer.WaitForNextTickAsync(ct))
                {
                    try
                    {
                        await action(ct);
                    }
                    catch (OperationCanceledException) when (ct.IsCancellationRequested)
                    {
                        break;
                    }
                    catch (Exception ex)
                    {
                        _logger.LogError(ex, "Error during scheduled job {Key} execution.", key);
                    }
                }
            }
        }
        catch (OperationCanceledException)
        {
        }
        finally
        {
            Cancel(key);
        }
    }
}
