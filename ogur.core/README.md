Core services for OgurBot:
- `Security`: `EncryptionManager` + `EncryptionOptions`
- `Configuration`: `JsonSettingsStore` (user-scoped JSON under `%AppData%/Ogur/Fishing/config.user.json`)
- `Scheduler`: `IScheduler` + `DefaultScheduler`
- DI extension: `AddOgurCore(IConfiguration)`