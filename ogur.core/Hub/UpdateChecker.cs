// File: Ogur.Core/Hub/UpdateChecker.cs
// Project: Ogur.Core
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Abstractions.Hub;

namespace Ogur.Core.Hub;

/// <summary>
/// Checks for application updates from the hub REST API.
/// </summary>
public sealed class UpdateChecker : IUpdateChecker
{
    private readonly HttpClient _httpClient;
    private readonly HubOptions _options;
    private readonly ILogger<UpdateChecker> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="UpdateChecker"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for API calls.</param>
    /// <param name="options">Hub configuration options.</param>
    /// <param name="logger">Logger instance.</param>
    public UpdateChecker(
        HttpClient httpClient,
        IOptions<HubOptions> options,
        ILogger<UpdateChecker> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<UpdateCheckResult> CheckForUpdatesAsync(string currentVersion, CancellationToken ct)
    {
        try
        {
            var url = $"{_options.HubUrl}/api/updates/check?application={Uri.EscapeDataString(_options.ApplicationName)}&version={Uri.EscapeDataString(currentVersion)}";
            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Update check failed: {StatusCode}", response.StatusCode);
                return UpdateCheckResult.NotAvailable(currentVersion);
            }

            var updateInfo = await response.Content.ReadFromJsonAsync<UpdateCheckResponse>(cancellationToken: ct);
            if (updateInfo is null || !updateInfo.IsUpdateAvailable)
            {
                _logger.LogInformation("No updates available for version {Version}", currentVersion);
                return UpdateCheckResult.NotAvailable(currentVersion);
            }

            _logger.LogInformation("Update available: {CurrentVersion} -> {LatestVersion} (Required: {IsRequired})", 
                currentVersion, updateInfo.LatestVersion, updateInfo.IsRequired);

            return UpdateCheckResult.Available(
                currentVersion,
                updateInfo.LatestVersion,
                updateInfo.DownloadUrl,
                updateInfo.ReleaseNotes,
                updateInfo.IsRequired);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update check failed with exception");
            return UpdateCheckResult.NotAvailable(currentVersion);
        }
    }

    private sealed record UpdateCheckResponse(
        bool IsUpdateAvailable,
        string LatestVersion,
        string DownloadUrl,
        string? ReleaseNotes,
        bool IsRequired);
}