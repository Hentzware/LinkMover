namespace LinkMover.Core.Models;

public sealed record LockingProcessInfo(
    int ProcessId,
    string ProcessName,
    string ExecutablePath,
    RestartManagerAppType AppType);
