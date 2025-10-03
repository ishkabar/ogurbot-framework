namespace ogur.core;
using ogur.abstractions;

internal static class LinkCheck
{
    public static (CapabilityStatus Status, BotEvent Event) Ping()
        => (CapabilityStatus.Running, new BotEvent(DateTimeOffset.UtcNow, "ping", "ok"));
}