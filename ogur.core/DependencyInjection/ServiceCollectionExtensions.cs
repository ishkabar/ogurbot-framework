// File: Ogur.Core/DependencyInjection/ServiceCollectionExtensions.cs
// Project: Ogur.Core

using System;
using System.Net.Http;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Ogur.Abstractions.Configuration;
using Ogur.Abstractions.Hub;
using Ogur.Abstractions.Security;
using Ogur.Abstractions.Memory;
using Ogur.Core.Configuration;
using Ogur.Core.Hub;
using Ogur.Core.Scheduler;
using Ogur.Core.Security;
using Ogur.Core.Metin.Memory; 

namespace Ogur.Core.DependencyInjection;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddOgurCore(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<EncryptionOptions>(configuration.GetSection("Encryption"));
        services.Configure<JsonSettingsStoreOptions>(configuration.GetSection("SettingsStore"));
        
        services.AddSingleton<IEncryptionManager, EncryptionManager>();
        services.AddSingleton<ISettingsStore, JsonSettingsStore>();
        services.AddSingleton<IScheduler, DefaultScheduler>();
        
        services.Configure<ChatDetectionOptions>(configuration.GetSection("ChatDetection"));
        services.AddSingleton<IChatBufferDetector, DifferentialChatBufferDetector>();
        
        return services;
    }

    public static IServiceCollection AddOgurHub(this IServiceCollection services, IConfiguration configuration)
    {
        services.Configure<HubOptions>(configuration.GetSection("Hub"));

        services.AddSingleton<IDeviceFingerprintProvider, DeviceFingerprintProvider>();
        
        services.AddSingleton<IAuthService>(sp =>
        {
            var httpClient = new HttpClient();
            var options = sp.GetRequiredService<IOptions<HubOptions>>().Value;
            httpClient.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
            
            var logger = sp.GetRequiredService<ILogger<AuthService>>();
            var optionsWrapper = sp.GetRequiredService<IOptions<HubOptions>>();
            
            return new AuthService(httpClient, optionsWrapper, logger);
        });
        
        services.AddSingleton<ILicenseValidator>(sp =>
        {
            var httpClient = new HttpClient();
            var options = sp.GetRequiredService<IOptions<HubOptions>>().Value;
            httpClient.Timeout = TimeSpan.FromSeconds(options.RequestTimeoutSeconds);
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", options.ApiKey);
            
            var optionsWrapper = sp.GetRequiredService<IOptions<HubOptions>>();
            var fingerprint = sp.GetRequiredService<IDeviceFingerprintProvider>();
            var authService = sp.GetRequiredService<IAuthService>();
            var logger = sp.GetRequiredService<ILogger<LicenseValidator>>();
            
            return new LicenseValidator(httpClient, optionsWrapper, fingerprint, authService, logger);
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