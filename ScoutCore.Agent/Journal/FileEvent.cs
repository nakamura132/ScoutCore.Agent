namespace ScoutCore.Agent.Journal;

public record FileEvent(
    DateTimeOffset Time,
    string FileKey,
    string? FromPath,
    string? ToPath,
    FileOpKind Kind,
    int? ProcessId,
    string? ProcessName,
    string? UserSid
);
