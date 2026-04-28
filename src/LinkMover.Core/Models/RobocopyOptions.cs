namespace LinkMover.Core.Models;

public sealed record RobocopyOptions(
    bool Mirror = true,
    bool DryRun = false,
    int Retries = 1,
    int WaitSeconds = 1);
