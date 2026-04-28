namespace LinkMover.Core.Models;

public sealed record DriveInfoSnapshot(
    string Root,
    long TotalBytes,
    long FreeBytes,
    string FileSystem,
    bool IsNtfs);
