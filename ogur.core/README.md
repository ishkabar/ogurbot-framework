# Ogur.Core

[![wakatime](https://wakatime.com/badge/github/ishkabar/ogurbot-framework.svg?style=flat-square)](https://wakatime.com/badge/github/ishkabar/ogurbot-framework)
![.NET](https://img.shields.io/badge/.NET-8.0-512BD4?style=flat-square&logo=dotnet)
![C#](https://img.shields.io/badge/C%23-12.0-239120?style=flat-square&logo=csharp)

Production implementations of `Ogur.Abstractions` contracts for Hub integration, security, and Metin2 utilities.

See [main repository README](../README.md) for full documentation and usage examples.

## Key Components

### Hub Integration
- `AuthService` - JWT authentication
- `LicenseValidator` - License validation (1 license = 2 devices)
- `UpdateChecker` - Version checking with forced updates
- `TelemetryReporter` - Event telemetry
- `HubClient` - SignalR real-time commands
- `DeviceFingerprintProvider` - HWID + GUID generation

### Core Services
- `EncryptionManager` - AES-CBC encryption
- `JsonSettingsStore` - User settings persistence
- `DefaultScheduler` - Task scheduling

### Metin2 Utilities
- `DifferentialChatBufferDetector` - Automatic chat buffer detection
- `KeyboardCompat` - Input compatibility layer
- `LegacyButtonKeyboardSynthesizer` - Legacy input adapter

## Installation
```bash
dotnet add package Ogur.Core --version 0.2.1-alpha
```

## Quick Start
```csharp
using Ogur.Core.DependencyInjection;

builder.Services.AddOgurCore(configuration);
builder.Services.AddOgurHub(configuration);
```

## License
MIT License © Ogur Project / Dominik Karczewski
