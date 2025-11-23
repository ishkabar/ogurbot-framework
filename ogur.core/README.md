# Ogur.Core

![Build](https://img.shields.io/badge/build-passing-brightgreen)
![License](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)
![Version](https://img.shields.io/badge/version-0.2.1--alpha-orange)

## Overview

**Ogur.Core** provides concrete implementations of core services defined in `Ogur.Abstractions`, including Hub integration, security, configuration management, and Metin2-specific utilities.

## Features

### Hub Integration
- **AuthService**: JWT authentication for web panel users
- **LicenseValidator**: License validation with automatic device registration (1 license = 2 devices)
- **UpdateChecker**: Version checking with required update enforcement
- **TelemetryReporter**: Application event and usage telemetry
- **HubClient**: SignalR real-time connection for remote commands
- **DeviceFingerprintProvider**: HWID (CPU + Motherboard + MAC) and persistent GUID

### Security & Configuration
- **EncryptionManager**: AES-CBC encryption with PBKDF2 key derivation
- **JsonSettingsStore**: User-scoped JSON configuration (`%AppData%/Ogur/[AppName]/config.user.json`)

### Scheduling
- **DefaultScheduler**: Recurring and delayed task execution with cancellation support

### Metin2 Memory Detection
- **DifferentialChatBufferDetector**: Automatic chat buffer detection via differential memory scanning
    - Takes 100 snapshots at 50ms intervals
    - Compares byte-by-byte changes to identify frequently modified regions
    - Validates using Metin2 color code pattern (`|cff`)
    - Returns MessageStart and Digit addresses for bite detection
    - Supports Polish characters (Windows-1250 encoding)

## Architecture

### Hub Integration Flow
```
Application Startup
    |
    v
AuthService.LoginAsync(username, password)
    |
    v
LicenseValidator.ValidateAsync()
    |-> Device Registration (HWID + GUID)
    |-> License Check (max 2 devices)
    v
UpdateChecker.CheckForUpdatesAsync()
    |-> Required Update? Block startup
    v
HubClient.ConnectAsync()
    |-> SignalR connection
    |-> Listen for commands (Logout, Block, Notify, ForceUpdate)
    v
Application Running
```

### Chat Detection Algorithm
```
DifferentialChatBufferDetector
    |
    v
Take 100 memory snapshots (50ms intervals)
    |
    v
Compare snapshots byte-by-byte
    |
    v
Group changed bytes into contiguous regions
    |
    v
Test offsets +9 and +10 for |cff pattern
    |
    v
Return top region by change count
    |
    v
ChatBufferInfo(MessageStart, DigitAddress, ChangeCount)
```

## Usage

### Registration
```csharp
using Microsoft.Extensions.DependencyInjection;
using Ogur.Core.DependencyInjection;

var services = new ServiceCollection();

// Register core services
services.AddOgurCore(configuration);

// Register Hub integration
services.AddOgurHub(configuration);
```

### Configuration
```json
{
  "Hub": {
    "HubUrl": "https://api.hub.ogur.dev",
    "ApiKey": "your-api-key",
    "ApplicationName": "OgurFishing",
    "RequestTimeoutSeconds": 30
  },
  "Encryption": {
    "MasterPassword": "your-master-password"
  },
  "SettingsStore": {
    "FileName": "config.user.json"
  },
  "ChatDetection": {
    "ScanStart": 214958080,
    "ScanEnd": 224395264,
    "SnapshotCount": 100,
    "IntervalMs": 50,
    "MinChangeCount": 10,
    "RegionGroupingGap": 1024,
    "ReadChunkSize": 4096
  }
}
```

### Authentication Example
```csharp
public class Application
{
    private readonly IAuthService _authService;
    private readonly ILicenseValidator _licenseValidator;
    private readonly IUpdateChecker _updateChecker;
    private readonly IHubClient _hubClient;

    public async Task StartAsync(CancellationToken ct)
    {
        // 1. Authenticate user
        var authResult = await _authService.LoginAsync(username, password, ct);
        if (!authResult.IsSuccess)
        {
            throw new InvalidOperationException("Authentication failed");
        }

        // 2. Check for required updates
        var updateResult = await _updateChecker.CheckForUpdatesAsync(version, ct);
        if (updateResult.IsUpdateAvailable && updateResult.IsRequired)
        {
            // Show update UI and exit
            Application.Current.Shutdown();
            return;
        }

        // 3. Validate license with device registration
        var licenseResult = await _licenseValidator.ValidateAsync(ct);
        if (!licenseResult.IsValid)
        {
            throw new InvalidOperationException($"License error: {licenseResult.ErrorMessage}");
        }

        // 4. Connect to Hub for real-time commands
        await _hubClient.ConnectAsync(ct);

        // 5. Application ready
    }
}
```

### Chat Detection Example
```csharp
public class FishingBot
{
    private readonly IChatBufferDetector _chatDetector;

    public async Task DetectChatBufferAsync(int processId, CancellationToken ct)
    {
        var result = await _chatDetector.DetectAsync(processId, ct);
        
        if (result is not null)
        {
            Console.WriteLine($"MessageStart: 0x{result.MessageStartAddress:X8}");
            Console.WriteLine($"Digit: 0x{result.DigitAddress:X8}");
            Console.WriteLine($"Changes: {result.ChangeCount}");
        }
        else
        {
            Console.WriteLine("Chat buffer not detected");
        }
    }
}
```

## Development

- **Language:** C# 12
- **Target Framework:** .NET 8
- **Project Type:** Class Library
- **Dependencies:**
    - Ogur.Abstractions (0.2.1-alpha)
    - Microsoft.AspNetCore.SignalR.Client
    - Microsoft.Extensions.Http
    - System.Text.Encoding.CodePages (Windows-1250 support)

## Package

Available as NuGet package for internal distribution.
```bash
dotnet add package Ogur.Core --version 0.2.1-alpha
```

## Version History

### 0.2.1-alpha
- Added AuthService for JWT authentication
- Added DifferentialChatBufferDetector for automatic memory detection
- Enhanced LicenseValidator with device registration (1 license = 2 devices)
- Enhanced UpdateChecker with required version enforcement
- Added ChatDetectionOptions for configurable scanning parameters
- Integrated IChatBufferDetector into service registration

### 0.2.0-alpha
- Initial Hub integration (HubClient, LicenseValidator, UpdateChecker, TelemetryReporter)
- Core services (EncryptionManager, JsonSettingsStore, DefaultScheduler)
- Device fingerprinting with HWID and GUID

## License

MIT License © Ogur Project / Dominik Karczewski