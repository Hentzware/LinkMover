namespace LinkMover.Core.Models;

public sealed record ScanProgress(
    string CurrentPath,
    long ScannedBytes,
    long ScannedItems);
