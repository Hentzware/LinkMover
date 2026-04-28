namespace LinkMover.Core.Models;

public sealed record JunctionInfo(
    string Source,
    string Target,
    long? TargetSizeBytes,
    DateTime Created,
    bool TargetExists);
