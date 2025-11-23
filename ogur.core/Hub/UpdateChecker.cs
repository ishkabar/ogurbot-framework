// File: Ogur.Core/Hub/UpdateChecker.cs
// Project: Ogur.Core

using System;
using System.Net.Http;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
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
            var url = $"{_options.HubUrl}/api/updates/check?currentVersion={Uri.EscapeDataString(currentVersion)}";

            var response = await _httpClient.GetAsync(url, ct);

            if (!response.IsSuccessStatusCode)
            {
                _logger.LogWarning("Update check failed: {StatusCode}", response.StatusCode);
                return UpdateCheckResult.NotAvailable(currentVersion);
            }

            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<UpdateCheckResponse>>(ct);
            
            var updateInfo = apiResponse?.Data;

            if (updateInfo is null || !updateInfo.UpdateAvailable)
            {
                _logger.LogInformation("No updates available for version {Version}", currentVersion);
                return UpdateCheckResult.NotAvailable(currentVersion);
            }

            _logger.LogInformation("Update available: {CurrentVersion} -> {LatestVersion} (Required: {IsRequired})", 
                currentVersion, updateInfo.LatestVersion, updateInfo.IsRequired);

            return UpdateCheckResult.Available(
                currentVersion,
                updateInfo.LatestVersion ?? currentVersion,
                updateInfo.DownloadUrl ?? string.Empty,
                updateInfo.ReleaseNotes,
                updateInfo.IsRequired);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Update check failed with exception");
            return UpdateCheckResult.NotAvailable(currentVersion);
        }
    }

    private sealed record ApiResponse<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
        public string? Error { get; init; }
    }

    private sealed record UpdateCheckResponse
    {
        public bool UpdateAvailable { get; init; }
        public string CurrentVersion { get; init; } = string.Empty;
        public string? LatestVersion { get; init; }
        public string? ReleaseNotes { get; init; }
        public string? DownloadUrl { get; init; }
        public bool IsRequired { get; init; }
        public DateTime? ReleasedAt { get; init; }
    }
}