# ogurbot-framework (Core)

Core library for **Ogur** bot framework (Metin2).  
Namespace: `ogur.core`. Targets **.NET 8**.

## What’s inside
- **Scheduler abstraction**
  - `IScheduler` – async delay abstraction
  - `DefaultScheduler` – implementation based on `Task.Delay`
- **Dependency on Abstractions** (`ogur.abstractions`)
  - Uses contracts like `IBotCapability`, `CapabilityStatus`, `BotEvent`

## Design
- **Clean layering**
  - Abstractions (`ogur.abstractions`) → Core (`ogur.core`) → Capabilities (e.g. Fishing) → Hosts (CLI/WPF)
- **Extensible**: Capabilities and hosts consume this Core via NuGet or project reference.
- **Testable**: logic separated from UI (WPF), async/await friendly, scheduler injectable.

## Quick start
Reference the package/project in your capability:

```csharp
using ogur.core;
using ogur.abstractions;

public sealed class MyBot
{
    private readonly IScheduler _scheduler = new DefaultScheduler();

    public async Task Run(CancellationToken ct)
    {
        Console.WriteLine("Starting bot...");
        await _scheduler.Delay(TimeSpan.FromSeconds(1), ct);
        Console.WriteLine("Done.");
    }
}
