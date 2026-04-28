using LinkMover.Core.Models;

namespace LinkMover.Core.Abstractions;

public interface IRestartManagerService
{
    Task<IReadOnlyList<LockingProcessInfo>> GetLockingProcessesAsync(IEnumerable<string> paths, CancellationToken ct);

    Task<bool> EndProcessesAsync(IEnumerable<int> processIds, CancellationToken ct);
}
