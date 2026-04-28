namespace LinkMover.Core.Models;

public sealed record RobocopyResult(
    int ExitCode,
    long FilesCopied,
    long BytesCopied,
    TimeSpan Elapsed,
    IReadOnlyList<string> Errors)
{
    public bool IsSuccess => ExitCode <= 7;
}
