namespace LinkMover.Core.Models;

public sealed record FileNode(
    string Name,
    long SizeBytes,
    string Extension);
