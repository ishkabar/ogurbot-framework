# Ogur.Core

![Build](https://img.shields.io/badge/build-passing-brightgreen)
![License](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)
![Version](https://img.shields.io/badge/version-0.2.0--alpha-orange)

## Overview

**Ogur.Core** provides the shared runtime infrastructure and implementation services for all Ogur applications and capabilities.

It includes:
- **Hub Integration:** SignalR client, license validation, update checking, telemetry reporting
- **Device Fingerprinting:** HWID (CPU+Motherboard) and persistent GUID generation
- **Scheduler:** Background task orchestration for recurring and delayed operations
- **Security:** AES-CBC encryption with PBKDF2 (200k iterations) key derivation
- **Configuration:** JSON-based settings store with per-application isolation
- **Dependency Injection:** Extensions for easy service registration

## Architecture

**Hub Integration (`Ogur.Core.Hub`):**
- `HubClient` — SignalR connection with automatic reconnection
- `LicenseValidator` — REST API calls for license validation with device registration
- `DeviceFingerprintProvider` — Hardware-based HWID + persistent GUID
- `UpdateChecker` — Version checking against hub
- `TelemetryReporter` — Batch telemetry reporting
- `HubOptions` — Centralized hub configuration

**Core Services:**
- `DefaultScheduler` — Lightweight recurring and one-shot task scheduling using `PeriodicTimer`
- `EncryptionManager` — Symmetric encryption for sensitive data at rest
- `JsonSettingsStore` — User-scoped configuration persistence with configurable application paths

**Dependency Injection:**
- `AddOgurCore()` — Registers encryption, settings, and scheduler
- `AddOgurHub()` — Registers hub client, license validator, telemetry, and HTTP clients

## Usage

### Basic Setup
```csharp
using Ogur.Core.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

// Add core services
builder.Services.AddOgurCore(builder.Configuration);

// Add hub integration
builder.Services.AddOgurHub(builder.Configuration);

var app = builder.Build();
await app.RunAsync();
```

### Configuration (appsettings.json)
```json
{
  "Hub": {
    "HubUrl": "https://hub.ogur.dev",
    "ApiKey": "your-application-api-key",
    "ApplicationName": "MyApp",
    "ApplicationVersion": "1.0.0",
    "EnableSignalR": true,
    "ReconnectDelaySeconds": 5,
    "RequestTimeoutSeconds": 30
  },
  "SettingsStore": {
    "ApplicationName": "MyApp",
    "FileName": "config.user.json"
  },
  "Encryption": {
    "EnvVarName": "OGUR_ENC_KEY"
  }
}
```

### License Validation
```csharp
public class MyApplication
{
    private readonly ILicenseValidator _licenseValidator;
    private readonly ILogger<MyApplication> _logger;

    public MyApplication(ILicenseValidator licenseValidator, ILogger<MyApplication> logger)
    {
        _licenseValidator = licenseValidator;
        _logger = logger;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        var result = await _licenseValidator.ValidateAsync(ct);
        
        if (!result.IsValid)
        {
            _logger.LogError("License validation failed: {Error}", result.ErrorMessage);
            throw new InvalidOperationException($"Invalid license: {result.ErrorMessage}");
        }

        _logger.LogInformation("License valid until {ExpiresAt}, Devices: {Registered}/{Max}",
            result.ExpiresAt, result.RegisteredDevices, result.MaxDevices);
    }
}
```

### Hub Connection with Commands
```csharp
public class HubService : BackgroundService
{
    private readonly IHubClient _hubClient;
    private readonly ILogger<HubService> _logger;

    public HubService(IHubClient hubClient, ILogger<HubService> logger)
    {
        _hubClient = hubClient;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var connectionResult = await _hubClient.ConnectAsync(ct);
        if (!connectionResult.IsSuccess)
        {
            _logger.LogError("Failed to connect to hub: {Error}", connectionResult.ErrorMessage);
            return;
        }

        await foreach (var command in _hubClient.ListenForCommandsAsync(ct))
        {
            _logger.LogInformation("Received command: {Type}", command.Type);
            
            switch (command.Type)
            {
                case HubCommandType.Logout:
                    _logger.LogWarning("Logout command received");
                    // Handle logout
                    break;
                    
                case HubCommandType.BlockDevice:
                    _logger.LogWarning("Device blocked by hub");
                    // Handle block
                    break;
                    
                case HubCommandType.Notify:
                    _logger.LogInformation("Notification: {Payload}", command.Payload);
                    break;
            }
        }
    }
}
```

### Scheduler
```csharp
public class HeartbeatService
{
    private readonly IScheduler _scheduler;
    private readonly ITelemetryReporter _telemetry;

    public HeartbeatService(IScheduler scheduler, ITelemetryReporter telemetry)
    {
        _scheduler = scheduler;
        _telemetry = telemetry;
    }

    public async Task StartAsync(CancellationToken ct)
    {
        await _scheduler.ScheduleRecurringAsync(
            "heartbeat",
            TimeSpan.FromMinutes(5),
            async (ct) =>
            {
                await _telemetry.ReportEventAsync("heartbeat", new { Status = "alive" }, ct);
            },
            ct);
    }
}
```

## Device Fingerprinting

The `DeviceFingerprintProvider` generates:
- **HWID:** SHA256 hash of CPU ID + Motherboard Serial (hardware-based, stable)
- **GUID:** Persistent GUID stored in `%AppData%\Ogur\.device` (survives reinstalls if file persists)

Fallback: If hardware IDs unavailable, generates hash from `MachineName|UserName|OSVersion`.

## Development

- **Language:** C# 12
- **Target Framework:** .NET 8
- **Dependencies:** 
  - Ogur.Abstractions 0.2.0-alpha
  - Microsoft.AspNetCore.SignalR.Client 8.0.11
  - System.Management 8.0.0
  - Microsoft.Extensions.* (Logging, Options, Configuration, Http)

## Package

Available as NuGet package for internal distribution.
```bash
dotnet add package Ogur.Core --version 0.2.0-alpha
```

## License

MIT License © Ogur Project / Dominik Karczewski