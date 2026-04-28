namespace LinkMover.Core.Models;

public sealed record JunctionPlan(
    string Source,
    string Target,
    bool DryRun = false,
    VerifyMode VerifyMode = VerifyMode.FileCountAndSize);
