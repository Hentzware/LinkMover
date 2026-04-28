using LinkMover.Core.Models;

namespace LinkMover.Core.Abstractions;

public interface IVerificationService
{
    Task<VerifyResult> VerifyAsync(string source, string target, VerifyMode mode, CancellationToken ct);
}
