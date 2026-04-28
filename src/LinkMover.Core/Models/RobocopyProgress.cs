namespace LinkMover.Core.Models;

public sealed record RobocopyProgress(
    string CurrentFile,
    double FilePercent,
    long TotalBytesCopied);
