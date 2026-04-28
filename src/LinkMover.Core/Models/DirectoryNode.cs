namespace LinkMover.Core.Models;

public sealed record DirectoryNode(
    string Path,
    long SizeBytes,
    long FileCount,
    IReadOnlyList<DirectoryNode> Children,
    IReadOnlyList<FileNode> Files);
