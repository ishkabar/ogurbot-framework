// File: Ogur.Core/Hub/LicenseValidator.cs
// Project: Ogur.Core

using System;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Threading;
using System.Threading.Tasks;
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
    private readonly IAuthService _authService;
    private readonly ILogger<LicenseValidator> _logger;

    /// <summary>
    /// Initializes a new instance of the <see cref="LicenseValidator"/> class.
    /// </summary>
    /// <param name="httpClient">HTTP client for API calls.</param>
    /// <param name="options">Hub configuration options.</param>
    /// <param name="fingerprintProvider">Device fingerprint provider.</param>
    /// <param name="authService">Authentication service for JWT token.</param>
    /// <param name="logger">Logger instance.</param>
    public LicenseValidator(
        HttpClient httpClient,
        IOptions<HubOptions> options,
        IDeviceFingerprintProvider fingerprintProvider,
        IAuthService authService,
        ILogger<LicenseValidator> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _fingerprintProvider = fingerprintProvider;
        _authService = authService;
        _logger = logger;
    }

    /// <inheritdoc />
    public async Task<LicenseValidationResult> ValidateAsync(CancellationToken ct)
    {
        if (!_authService.IsAuthenticated)
        {
            _logger.LogError("User not authenticated");
            return LicenseValidationResult.Invalid(LicenseValidationError.NotFound, "User not authenticated");
        }

        return await ValidateInternalAsync(ct);
    }

    /// <inheritdoc />
    public Task<LicenseValidationResult> ValidateAsync(string licenseKey, CancellationToken ct)
    {
        return ValidateInternalAsync(ct);
    }

    private async Task<LicenseValidationResult> ValidateInternalAsync(CancellationToken ct)
    {
        try
        {
            var fingerprint = await _fingerprintProvider.GetFingerprintAsync(ct);
            
            var request = new
            {
                Hwid = fingerprint.Hwid,
                DeviceGuid = Guid.Parse(fingerprint.Guid),
                DeviceName = fingerprint.DeviceName
            };

            var requestMessage = new HttpRequestMessage(HttpMethod.Post, $"{_options.HubUrl}/api/licenses/validate")
            {
                Content = JsonContent.Create(request)
            };

            requestMessage.Headers.Add("X-Api-Key", _options.ApiKey);
            
            if (!string.IsNullOrEmpty(_authService.AccessToken))
            {
                requestMessage.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _authService.AccessToken);
            }

            var response = await _httpClient.SendAsync(requestMessage, ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("License validation failed: {StatusCode} - {Error}", response.StatusCode, errorContent);
                
                return response.StatusCode switch
                {
                    System.Net.HttpStatusCode.NotFound => LicenseValidationResult.Invalid(
                        LicenseValidationError.NotFound, 
                        "No valid license found for this user"),  // ← 404
                    System.Net.HttpStatusCode.Forbidden => LicenseValidationResult.Invalid(
                        LicenseValidationError.DeviceBlocked, 
                        "Device is blocked"),
                    System.Net.HttpStatusCode.Unauthorized => LicenseValidationResult.Invalid(
                        LicenseValidationError.NotFound, 
                        "Not authenticated"),
                    _ => LicenseValidationResult.Invalid(
                        LicenseValidationError.Inactive, 
                        errorContent)
                };
            }

            

            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<LicenseValidationResponse>>(cancellationToken: ct);
            if (apiResponse?.Data is null)
            {
                _logger.LogError("Failed to deserialize license validation response");
                return LicenseValidationResult.Invalid(LicenseValidationError.Inactive, apiResponse?.Error ?? "Invalid response from hub");
            }

            var result = apiResponse.Data;

            if (!result.IsValid)
            {
                _logger.LogWarning("License validation returned IsValid=false: {Error}", result.ErrorMessage);
                return LicenseValidationResult.Invalid(
                    LicenseValidationError.NotFound, 
                    result.ErrorMessage ?? "License validation failed");
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

    private sealed record LicenseValidationResponse
    {
        public bool IsValid { get; init; }  // ← BEZ required
        public int? DeviceId { get; init; }
        public bool IsNewDevice { get; init; }
        public DateTime? ExpiresAt { get; init; }
        public int RegisteredDevices { get; init; }
        public int MaxDevices { get; init; }
        public string? ErrorMessage { get; init; }
    }
    
    private sealed record ApiResponse<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
        public string? Error { get; init; }
    }
}