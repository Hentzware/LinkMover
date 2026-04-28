using LinkMover.Core.Models;

namespace LinkMover.Core.Abstractions;

public interface IRobocopyService
{
    Task<RobocopyResult> CopyAsync(
        string source,
        string target,
        RobocopyOptions options,
        IProgress<RobocopyProgress>? progress,
        CancellationToken ct);
}
