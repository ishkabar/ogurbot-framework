// File: Ogur.Core/Hub/AuthService.cs
// Project: Ogur.Core
// Namespace: Ogur.Core.Hub

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
/// Hub authentication service implementation.
/// </summary>
public sealed class AuthService : IAuthService
{
    private readonly HttpClient _httpClient;
    private readonly HubOptions _options;
    private readonly ILogger<AuthService> _logger;
    
    private string? _accessToken;
    private int? _userId;
    private string? _username;
    private bool _isAdmin;

    /// <summary>
    /// Initializes a new instance of the <see cref="AuthService"/> class.
    /// </summary>
    public AuthService(
        HttpClient httpClient,
        IOptions<HubOptions> options,
        ILogger<AuthService> logger)
    {
        _httpClient = httpClient;
        _options = options.Value;
        _logger = logger;
    }

    /// <inheritdoc />
    public string? AccessToken => _accessToken;

    /// <inheritdoc />
    public bool IsAuthenticated => !string.IsNullOrEmpty(_accessToken);

    /// <inheritdoc />
    public int? UserId => _userId;

    /// <inheritdoc />
    public string? Username => _username;

    /// <inheritdoc />
    public bool IsAdmin => _isAdmin;

    /// <inheritdoc />
    public async Task<AuthResult> LoginAsync(string username, string password, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Attempting login for user: {Username}", username);

            var request = new
            {
                Username = username,
                Password = password
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_options.HubUrl}/api/Auth/login",
                request,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Login failed for user {Username}: {Error}", username, errorContent);
                
                return AuthResult.Failed(errorContent);
            }

            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<LoginResponseDto>>(ct);
            
            if (apiResponse?.Data == null)
            {
                return AuthResult.Failed("Invalid response from server");
            }

            var data = apiResponse.Data;
            
            // Store authentication state
            _accessToken = data.AccessToken;
            _userId = data.UserId;
            _username = data.Username;
            _isAdmin = data.IsAdmin;

            _logger.LogInformation("User {Username} logged in successfully (Admin: {IsAdmin})", username, _isAdmin);

            return AuthResult.Succeeded(
                data.AccessToken,
                data.TokenType ?? "Bearer",
                data.ExpiresIn,
                data.UserId,
                data.Username,
                data.IsAdmin,
                data.Role
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login request failed for user: {Username}", username);
            return AuthResult.Failed($"Login failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public async Task<RegisterResult> RegisterAsync(string username, string password, string? email = null, CancellationToken ct = default)
    {
        try
        {
            _logger.LogInformation("Attempting registration for user: {Username}", username);

            var request = new
            {
                Username = username,
                Password = password,
                Email = email
            };

            var response = await _httpClient.PostAsJsonAsync(
                $"{_options.HubUrl}/api/Auth/register",
                request,
                ct);

            if (!response.IsSuccessStatusCode)
            {
                var errorContent = await response.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Registration failed for user {Username}: {Error}", username, errorContent);
                
                return RegisterResult.Failed(errorContent);
            }

            var apiResponse = await response.Content.ReadFromJsonAsync<ApiResponse<RegisterResponseDto>>(ct);
            
            if (apiResponse?.Data == null)
            {
                return RegisterResult.Failed("Invalid response from server");
            }

            var data = apiResponse.Data;

            _logger.LogInformation("User {Username} registered successfully (ID: {UserId})", username, data.UserId);

            return RegisterResult.Succeeded(
                data.UserId,
                data.Username,
                data.Message
            );
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Registration request failed for user: {Username}", username);
            return RegisterResult.Failed($"Registration failed: {ex.Message}");
        }
    }

    /// <inheritdoc />
    public void Logout()
    {
        _logger.LogInformation("User {Username} logged out", _username);
        
        _accessToken = null;
        _userId = null;
        _username = null;
        _isAdmin = false;
    }

    // DTOs matching API schema
    private sealed record ApiResponse<T>
    {
        public bool Success { get; init; }
        public T? Data { get; init; }
        public string? Error { get; init; }
        public DateTime Timestamp { get; init; }
    }

    private sealed record LoginResponseDto
    {
        public string AccessToken { get; init; } = string.Empty;
        public string? TokenType { get; init; }
        public int ExpiresIn { get; init; }
        public int UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public bool IsAdmin { get; init; }
        public int Role { get; init; }
    }

    private sealed record RegisterResponseDto
    {
        public int UserId { get; init; }
        public string Username { get; init; } = string.Empty;
        public string Message { get; init; } = string.Empty;
    }
}