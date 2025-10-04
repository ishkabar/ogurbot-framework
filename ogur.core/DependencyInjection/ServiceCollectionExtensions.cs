using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Ogur.Abstractions.Configuration;
using Ogur.Abstractions.Security;
using Ogur.Core.Configuration;
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
        services.AddSingleton<IEncryptionManager, EncryptionManager>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IScheduler, DefaultScheduler>();
        return services;
    }
}
