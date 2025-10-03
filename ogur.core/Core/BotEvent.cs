namespace ogur.core.Core;

public readonly record struct BotEvent(DateTimeOffset Timestamp, string Type, string Message);
