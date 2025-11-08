// File: Ogur.Core/Hub/TelemetryReporter.cs
// Project: Ogur.Core
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Abstractions.Hub;

namespace Ogur.Core.Hub;

/// <summary>
/// Reports telemetry data to the hub REST API.
/// </summary>
public sealed class TelemetryReporter : ITelemetryReporter
{
    private readonly HttpClient _httpClient;
    private readonly HubOptions _options;
    private readonly IDeviceFingerprintProvider _fingerprintProvider;
    private readonly ILogger<TelemetryReporter> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="TelemetryReporter"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for API calls.</param>
    /// <param name="options">Hub configuration options.</param>
    /// <param name="fingerprintProvider">Device fingerprint provider.</param>
    /// <param name="logger">Logger instance.</param>
    public TelemetryReporter(
        HttpClient httpClient,
        IOptions<HubOptions> options,
        IDeviceFingerprintProvider fingerprintProvider,
        ILogger<TelemetryReporter> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _fingerprintProvider = fingerprintProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task ReportEventAsync(string eventType, object? eventData, CancellationToken ct)
    {
        var telemetryEvent = new TelemetryEvent(eventType, eventData, DateTimeOffset.UtcNow);
        await ReportBatchAsync(new[] { telemetryEvent }, ct);
    }

    /// <inheritdoc />
    public async Task ReportBatchAsync(IEnumerable<TelemetryEvent> events, CancellationToken ct)
    {
        try
        {
            var fingerprint = await _fingerprintProvider.GetFingerprintAsync(ct);
            
            var request = new
            {
                ApplicationName = _options.ApplicationName,
                DeviceGuid = fingerprint.Guid,
                Events = events.Select(e => new
                {
                    EventType = e.EventType,
                    EventData = e.EventData,
                    Timestamp = e.Timestamp
                }).ToList()
            };

            var response = await _httpClient.PostAsJsonAsync($"{_options.HubUrl}/api/telemetry", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Telemetry reporting failed: {StatusCode}", response.StatusCode);
            }
            else
            {
                _logger.LogDebug("Telemetry batch reported successfully: {EventCount} events", events.Count());
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Telemetry reporting failed with exception");
        }
    }
}