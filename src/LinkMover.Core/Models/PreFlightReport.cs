namespace LinkMover.Core.Models;

public sealed record PreFlightReport(IReadOnlyList<PreFlightCheck> Checks)
{
    public bool IsBlocking => Checks.Any(c => c.Severity == Severity.Error);
}
