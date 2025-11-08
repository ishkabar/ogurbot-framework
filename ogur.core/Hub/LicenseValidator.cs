// File: Ogur.Core/Hub/LicenseValidator.cs
// Project: Ogur.Core
using System.Net.Http.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Abstractions.Hub;

namespace Ogur.Core.Hub;

/// <summary>
/// Validates licenses against the hub REST API.
/// </summary>
public sealed class LicenseValidator : ILicenseValidator
{
    private readonly HttpClient _httpClient;
    private readonly HubOptions _options;
    private readonly IDeviceFingerprintProvider _fingerprintProvider;
    private readonly ILogger<LicenseValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseValidator"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for API calls.</param>
    /// <param name="options">Hub configuration options.</param>
    /// <param name="fingerprintProvider">Device fingerprint provider.</param>
    /// <param name="logger">Logger instance.</param>
    public LicenseValidator(
        HttpClient httpClient,
        IOptions<HubOptions> options,
        IDeviceFingerprintProvider fingerprintProvider,
        ILogger<LicenseValidator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _fingerprintProvider = fingerprintProvider;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LicenseValidationResult> ValidateAsync(CancellationToken ct)
    {
        var licenseKey = _options.ApiKey;
        if (string.IsNullOrWhiteSpace(licenseKey))
        {
            _logger.LogError("License key not configured");
            return LicenseValidationResult.Invalid(LicenseValidationError.NotFound, "License key not configured");
        }

        return await ValidateAsync(licenseKey, ct);
    }

    /// <inheritdoc />
    public async Task<LicenseValidationResult> ValidateAsync(string licenseKey, CancellationToken ct)
    {
        try
        {
            var fingerprint = await _fingerprintProvider.GetFingerprintAsync(ct);
            
            var request = new
            {
                LicenseKey = licenseKey,
                ApplicationName = _options.ApplicationName,
                DeviceHwid = fingerprint.Hwid,
                DeviceGuid = fingerprint.Guid,
                DeviceName = fingerprint.DeviceName
            };

            var response = await _httpClient.PostAsJsonAsync($"{_options.HubUrl}/api/licenses/validate", request, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("License validation failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                
                return response.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound => LicenseValidationResult.Invalid(LicenseValidationError.NotFound, "License not found"),
                    System.Net.HttpStatusCode.Forbidden => LicenseValidationResult.Invalid(LicenseValidationError.DeviceBlocked, "Device is blocked"),
                    _ => LicenseValidationResult.Invalid(LicenseValidationError.Inactive, errorContent)
                };
            }

            var result = await response.Content.ReadFromJsonAsync<LicenseValidationResponse>(cancellationToken: ct);
            if (result is null)
            {
                _logger.LogError("Failed to deserialize license validation response");
                return LicenseValidationResult.Invalid(LicenseValidationError.Inactive, "Invalid response from hub");
            }

            _logger.LogInformation("License validated successfully: Expires={ExpiresAt}, Devices={Registered}/{Max}", 
                result.ExpiresAt, result.RegisteredDevices, result.MaxDevices);

            return LicenseValidationResult.Valid(result.ExpiresAt, result.MaxDevices, result.RegisteredDevices);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "License validation failed with exception");
            return LicenseValidationResult.Invalid(LicenseValidationError.Inactive, $"Validation error: {ex.Message}");
        }
    }

    private sealed record LicenseValidationResponse(DateTime? ExpiresAt, int MaxDevices, int RegisteredDevices);
}