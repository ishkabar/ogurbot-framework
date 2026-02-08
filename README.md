# Ogur.Core

[![wakatime](https://wakatime.com/badge/github/ishkabar/ogurbot-framework.svg?style=flat-square)](https://wakatime.com/badge/github/ishkabar/ogurbot-framework)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![C#](https://img.shields.io/badge/C%23-12.0-239120?style=flat-square&logo=csharp)
![Version](https://img.shields.io/badge/version-0.2.1--alpha-orange?style=flat-square)
![License](https://img.shields.io/badge/license-MIT-blue?style=flat-square)

Concrete implementations of `Ogur.Abstractions` services, including Hub integration, security, configuration management, and Metin2-specific utilities.

## Overview

**Ogur.Core** provides production-ready implementations for:
- **Hub Integration**: JWT authentication, license validation, updates, telemetry, SignalR commands
- **Security**: AES-CBC encryption with PBKDF2 key derivation
- **Configuration**: JSON-based user settings with per-application isolation
- **Scheduling**: Recurring and delayed task execution
- **Metin2 Memory Detection**: Automatic chat buffer detection via differential scanning

## Features

### Hub Integration
```csharp
AuthService                  // JWT authentication for web panel users
LicenseValidator             // License validation (1 license = 2 devices max)
UpdateChecker                // Version checking with forced update enforcement
TelemetryReporter            // Application event and usage telemetry
HubClient                    // SignalR real-time connection for remote commands
DeviceFingerprintProvider    // HWID (CPU + Motherboard + MAC) + persistent GUID
```

**Hub Command Types:**
- `Logout` - Force user logout
- `BlockDevice` - Block current device
- `Notify` - Display notification
- `ForceUpdate` - Require immediate update

### Security & Configuration
```csharp
EncryptionManager            // AES-CBC encryption with PBKDF2 (200k iterations)
JsonSettingsStore            // User-scoped JSON config (%AppData%/Ogur/[AppName]/config.user.json)
```

### Scheduling
```csharp
DefaultScheduler             // Recurring and delayed task execution with cancellation
```

### Metin2 Memory Detection
```csharp
DifferentialChatBufferDetector  // Automatic chat buffer detection
```

**Detection Algorithm:**
1. Take 100 memory snapshots at 50ms intervals
2. Compare byte-by-byte changes to identify frequently modified regions
3. Group changed bytes into contiguous regions (1024-byte gap threshold)
4. Validate regions using Metin2 color code pattern (`|cff` at offset +9/+10)
5. Return region with highest change count as MessageStart
6. Calculate Digit address as MessageStart + 10

**Supports:**
- Polish characters (Windows-1250 encoding)
- Configurable scan range, intervals, and thresholds
- Parallel snapshot processing

## Architecture

### Hub Integration Flow
```
Application Startup
    ↓
AuthService.LoginAsync(username, password)
    ↓
LicenseValidator.ValidateAsync()
    ├→ Device Registration (HWID + GUID)
    └→ License Check (max 2 devices)
    ↓
UpdateChecker.CheckForUpdatesAsync()
    └→ Required Update? Block startup
    ↓
HubClient.ConnectAsync()
    ├→ SignalR connection
    └→ Listen for commands (Logout, Block, Notify, ForceUpdate)
    ↓
Application Running
```

### Chat Detection Flow
```
DifferentialChatBufferDetector
    ↓
Take 100 snapshots (50ms intervals)
    ↓
Compare snapshots byte-by-byte
    ↓
Group changed bytes into contiguous regions
    ↓
Test offsets +9 and +10 for |cff pattern
    ↓
Return region with highest change count
    ↓
ChatBufferInfo(MessageStart, DigitAddress, ChangeCount)
```

## Installation
```bash
dotnet add package Ogur.Core --version 0.2.1-alpha
```

Or add to `.csproj`:
```xml
<PackageReference Include="Ogur.Core" Version="0.2.1-alpha" />
```

## Usage

### Service Registration
```csharp
using Ogur.Core.DependencyInjection;

var builder = Host.CreateApplicationBuilder(args);

// Register core services (encryption, settings, scheduler)
builder.Services.AddOgurCore(builder.Configuration);

// Register Hub integration (auth, license, telemetry, SignalR)
builder.Services.AddOgurHub(builder.Configuration);

var app = builder.Build();
await app.RunAsync();
```

### Configuration (appsettings.json)
```json
{
  "Hub": {
    "HubUrl": "https://api.hub.ogur.dev",
    "ApiKey": "your-api-key",
    "ApplicationName": "OgurFishing",
    "ApplicationVersion": "1.0.0",
    "EnableSignalR": true,
    "ReconnectDelaySeconds": 5,
    "RequestTimeoutSeconds": 30
  },
  "Encryption": {
    "EnvVarName": "OGUR_ENC_KEY"
  },
  "SettingsStore": {
    "ApplicationName": "MyApp",
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

### Authentication Flow
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
            throw new InvalidOperationException("Authentication failed");

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
            throw new InvalidOperationException($"License error: {licenseResult.ErrorMessage}");

        // 4. Connect to Hub for real-time commands
        await _hubClient.ConnectAsync(ct);

        // 5. Application ready
    }
}
```

### Hub Command Handling
```csharp
public class HubService : BackgroundService
{
    private readonly IHubClient _hubClient;
    private readonly ILogger<HubService> _logger;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        var connectionResult = await _hubClient.ConnectAsync(ct);
        if (!connectionResult.IsSuccess)
        {
            _logger.LogError("Failed to connect: {Error}", connectionResult.ErrorMessage);
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
                    _logger.LogWarning("Device blocked");
                    // Handle block
                    break;
                    
                case HubCommandType.Notify:
                    _logger.LogInformation("Notification: {Payload}", command.Payload);
                    break;
                    
                case HubCommandType.ForceUpdate:
                    _logger.LogWarning("Force update required");
                    // Handle update
                    break;
            }
        }
    }
}
```

### Chat Buffer Detection
```csharp
public class FishingBot
{
    private readonly IChatBufferDetector _chatDetector;
    private readonly ILogger<FishingBot> _logger;

    public async Task DetectChatBufferAsync(int processId, CancellationToken ct)
    {
        _logger.LogInformation("Starting chat buffer detection...");
        
        var result = await _chatDetector.DetectAsync(processId, ct);
        
        if (result is not null)
        {
            _logger.LogInformation(
                "Chat buffer detected: MessageStart=0x{MessageStart:X8}, Digit=0x{Digit:X8}, Changes={Changes}",
                result.MessageStartAddress,
                result.DigitAddress,
                result.ChangeCount);
        }
        else
        {
            _logger.LogWarning("Chat buffer not detected");
        }
    }
}
```

### Scheduler Usage
```csharp
public class HeartbeatService
{
    private readonly IScheduler _scheduler;
    private readonly ITelemetryReporter _telemetry;

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

## Project Structure
```
ogur.core/
├── Hub/                              # Hub integration services
│   ├── AuthService.cs                # JWT authentication
│   ├── LicenseValidator.cs           # License validation with device registration
│   ├── UpdateChecker.cs              # Version checking
│   ├── TelemetryReporter.cs          # Event telemetry
│   ├── HubClient.cs                  # SignalR connection
│   ├── DeviceFingerprintProvider.cs  # HWID + GUID generation
│   └── HubOptions.cs                 # Configuration model
├── Security/
│   ├── EncryptionManager.cs          # AES-CBC encryption
│   └── EncryptionOptions.cs          # Encryption config
├── Configuration/
│   └── JsonSettingsStore.cs          # User settings persistence
├── Metin/
│   ├── Memory/
│   │   ├── DifferentialChatBufferDetector.cs  # Auto-detection
│   │   └── ChatDetectionOptions.cs            # Detection config
│   ├── Input/
│   │   ├── KeyboardCompat.cs                  # Input compatibility
│   │   └── Adapters/
│   │       └── LegacyButtonKeyboardSynthesizer.cs
│   └── Legacy/
│       ├── Button.cs                          # Legacy input (Metin2 compat)
│       └── User.cs                            # Legacy user utilities
├── DependencyInjection/
│   └── ServiceCollectionExtensions.cs         # Service registration
└── DefaultScheduler.cs                        # Task scheduling
```

## Device Fingerprinting

**HWID Generation:**
1. Retrieve CPU ID via WMI (`Win32_Processor.ProcessorId`)
2. Retrieve Motherboard Serial via WMI (`Win32_BaseBoard.SerialNumber`)
3. Retrieve MAC Address via NetworkInterface
4. Compute SHA256 hash of combined values
5. Fallback: Hash `MachineName|UserName|OSVersion` if hardware IDs unavailable

**GUID Persistence:**
- Stored in `%AppData%\Ogur\.device`
- Survives application reinstalls if file persists
- Auto-generated on first run

## Version History

### 0.2.1-alpha
- Added `AuthService` for JWT authentication
- Added `DifferentialChatBufferDetector` for automatic memory detection
- Enhanced `LicenseValidator` with device registration (1 license = 2 devices)
- Enhanced `UpdateChecker` with required version enforcement
- Added `ChatDetectionOptions` for configurable scanning parameters
- Integrated `IChatBufferDetector` into service registration

### 0.2.0-alpha
- Initial Hub integration (HubClient, LicenseValidator, UpdateChecker, TelemetryReporter)
- Core services (EncryptionManager, JsonSettingsStore, DefaultScheduler)
- Device fingerprinting with HWID and GUID

## Dependencies
- `Ogur.Abstractions` (0.2.1-alpha)
- `Microsoft.AspNetCore.SignalR.Client` (8.0.11)
- `Microsoft.Extensions.Http` (8.0.1)
- `System.Text.Encoding.CodePages` (8.0.0) - Windows-1250 support
- `System.Management` (8.0.0) - WMI for HWID

## Build
```bash
dotnet build
dotnet pack
```

Or use included `pack.bat` for local NuGet packaging.

## License
MIT License © Ogur Project / Dominik Karczewski
