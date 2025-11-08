// File: Ogur.Core/Hub/HubOptions.cs
// Project: Ogur.Core
namespace Ogur.Core.Hub;

/// <summary>
/// Configuration options for hub connection.
/// </summary>
public sealed class HubOptions
{
    /// <summary>
    /// Gets or sets the hub base URL (e.g., https://hub.ogur.dev).
    /// </summary>
    public string HubUrl { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application API key for authentication.
    /// </summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the application name identifier.
    /// </summary>
    public string ApplicationName { get; set; } = string.Empty;

    /// <summary>
    /// Gets or sets the current application version.
    /// </summary>
    public string ApplicationVersion { get; set; } = "0.0.0";

    /// <summary>
    /// Gets or sets whether to enable SignalR connection.
    /// </summary>
    public bool EnableSignalR { get; set; } = true;

    /// <summary>
    /// Gets or sets the SignalR reconnect delay in seconds.
    /// </summary>
    public int ReconnectDelaySeconds { get; set; } = 5;

    /// <summary>
    /// Gets or sets the HTTP request timeout in seconds.
    /// </summary>
    public int RequestTimeoutSeconds { get; set; } = 30;
}