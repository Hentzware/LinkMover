using LinkMover.Core.Abstractions;
using LinkMover.Core.Models;

namespace LinkMover.Core.Services;

public sealed class VerificationService : IVerificationService
{
    private readonly IFileSystemService _fileSystem;

    public VerificationService(IFileSystemService fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task<VerifyResult> VerifyAsync(string source, string target, VerifyMode mode, CancellationToken ct)
    {
        if (mode == VerifyMode.RobocopyExitCodeOnly)
        {
            return new VerifyResult(
                Match: true,
                SourceFileCount: 0,
                TargetFileCount: 0,
                SourceBytes: 0,
                TargetBytes: 0,
                MissingFiles: Array.Empty<string>());
        }

        var sourceFiles = await Task.Run(
            () => new HashSet<string>(EnumerateRelative(source, ct), StringComparer.OrdinalIgnoreCase),
            ct);
        var targetFiles = await Task.Run(
            () => new HashSet<string>(EnumerateRelative(target, ct), StringComparer.OrdinalIgnoreCase),
            ct);

        var missing = sourceFiles.Where(f => !targetFiles.Contains(f)).ToList();

        long sourceBytes = 0;
        long targetBytes = 0;
        if (mode == VerifyMode.FileCountAndSize)
        {
            sourceBytes = await _fileSystem.GetDirectorySizeAsync(source, progress: null, ct);
            targetBytes = await _fileSystem.GetDirectorySizeAsync(target, progress: null, ct);
        }

        var match = missing.Count == 0
                    && (mode != VerifyMode.FileCountAndSize || sourceBytes == targetBytes);

        return new VerifyResult(
            Match: match,
            SourceFileCount: sourceFiles.Count,
            TargetFileCount: targetFiles.Count,
            SourceBytes: sourceBytes,
            TargetBytes: targetBytes,
            MissingFiles: missing);
    }

    private static IEnumerable<string> EnumerateRelative(string root, CancellationToken ct)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        foreach (var file in Directory.EnumerateFiles(root, "*", options))
        {
            ct.ThrowIfCancellationRequested();
            yield return Path.GetRelativePath(root, file);
        }
    }
}
