namespace LinkMover.Core.Models;

public sealed record VerifyResult(
    bool Match,
    long SourceFileCount,
    long TargetFileCount,
    long SourceBytes,
    long TargetBytes,
    IReadOnlyList<string> MissingFiles);
