namespace LinkMover.Core.Models;

public sealed record PreFlightCheck(
    string Name,
    Severity Severity,
    string Message);
