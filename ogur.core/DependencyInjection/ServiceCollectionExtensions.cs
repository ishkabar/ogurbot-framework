// File: Ogur.Core/DependencyInjection/ServiceCollectionExtensions.cs
// Project: Ogur.Core
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.Extensions.DependencyInjection;
using Ogur.Abstractions.Configuration;
using Ogur.Abstractions.Hub;
using Ogur.Abstractions.Security;
using Ogur.Core.Configuration;
using Ogur.Core.Hub;
using Ogur.Core.Scheduler;
using Ogur.Core.Security;

namespace Ogur.Core.DependencyInjection;

/// <summary>
/// DI registration helpers for Ogur.Core services.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Adds core services: <see cref="IEncryptionManager"/>, <see cref="ISettingsStore"/>, and <see cref="IScheduler"/>.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>Service collection.</returns>
    public static IServiceCollection AddOgurCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EncryptionOptions>(configuration.GetSection("Encryption"));
        services.Configure<JsonSettingsStoreOptions>(configuration.GetSection("SettingsStore"));
        
        services.AddSingleton<IEncryptionManager, EncryptionManager>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IScheduler, DefaultScheduler>();
        
        return services;
    }

    /// <summary>
    /// Adds hub integration services: license validation, updates, telemetry, and SignalR client.
    /// </summary>
    /// <param name="services">Service collection.</param>
    /// <param name="configuration">Application configuration.</param>
    /// <returns>Service collection.</returns>
    public static IServiceCollection AddOgurHub(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HubOptions>(configuration.GetSection("Hub"));

        services.AddSingleton<IDeviceFingerprintProvider, DeviceFingerprintProvider>();
        
        services.AddHttpClient<ILicenseValidator, LicenseValidator>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HubOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        });

        services.AddHttpClient<IUpdateChecker, UpdateChecker>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HubOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        });

        services.AddHttpClient<ITelemetryReporter, TelemetryReporter>((sp, client) =>
        {
            var options = sp.GetRequiredService<IOptions<HubOptions>>().Value;
            client.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
            client.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
        });

        services.AddSingleton<IHubClient, HubClient>();

        return services;
    }
}