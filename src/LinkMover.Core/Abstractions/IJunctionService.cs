using LinkMover.Core.Models;

namespace LinkMover.Core.Abstractions;

public interface IJunctionService
{
    Task<bool> CreateJunctionAsync(string source, string target, CancellationToken ct);

    Task<bool> RemoveJunctionAsync(string source);

    JunctionInfo? Inspect(string path);
}
