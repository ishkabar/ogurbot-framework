# Ogur.Core

![Build](https://img.shields.io/badge/build-passing-brightgreen)
![License](https://img.shields.io/badge/license-MIT-blue)
![.NET](https://img.shields.io/badge/.NET-8.0-blueviolet)

## Overview
**Ogur.Core** provides the shared runtime infrastructure for all Ogur bot capabilities.  
It includes the internal scheduler, state machine (FSM), and event pipeline supporting concurrent capability execution.

## Architecture
- `Scheduler` — background task orchestration using `BackgroundService`.
- `CapabilityStartContext` — provides DI-based runtime context for each capability.
- `BotEvent` and `CapabilityStatus` — define unified event models and lifecycle tracking.

## Usage
Ogur.Core is automatically referenced by capability projects like `Ogur.Capabilities.Fishing`.

```csharp
await scheduler.ScheduleAsync(fishingCapability, token);
```

## Development
- **Language:** C# 12  
- **Target Framework:** .NET 8  
- **Dependencies:** Ogur.Abstractions

## License
MIT License © Ogur Project
