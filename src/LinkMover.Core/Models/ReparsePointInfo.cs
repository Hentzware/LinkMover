namespace LinkMover.Core.Models;

public sealed record ReparsePointInfo(
    string Path,
    string? Target,
    ReparsePointType Type);
