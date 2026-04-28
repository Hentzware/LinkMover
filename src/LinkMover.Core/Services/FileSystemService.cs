using LinkMover.Core.Abstractions;
using LinkMover.Core.Models;

namespace LinkMover.Core.Services;

public sealed class FileSystemService : IFileSystemService
{
    public bool DirectoryExists(string path) => Directory.Exists(path);

    public bool IsReparsePoint(string path)
    {
        if (!Directory.Exists(path) && !File.Exists(path))
        {
            return false;
        }

        var attributes = File.GetAttributes(path);
        return (attributes & FileAttributes.ReparsePoint) != 0;
    }

    public string? ResolveLinkTarget(string path)
    {
        try
        {
            var target = Directory.ResolveLinkTarget(path, returnFinalTarget: false);
            return target?.FullName;
        }
        catch
        {
            return null;
        }
    }

    public Task<long> GetDirectorySizeAsync(string path, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        return Task.Run(() => SumSize(path, progress, ct), ct);
    }

    public Task<long> CountFilesAsync(string path, CancellationToken ct)
    {
        return Task.Run(() => CountFiles(path, ct), ct);
    }

    public DriveInfoSnapshot GetDrive(string path)
    {
        var root = Path.GetPathRoot(path)
                   ?? throw new ArgumentException("Pfad hat kein erkennbares Root-Volume.", nameof(path));
        var info = new DriveInfo(root);
        var format = info.IsReady ? info.DriveFormat : "Unknown";
        return new DriveInfoSnapshot(
            info.RootDirectory.FullName,
            info.IsReady ? info.TotalSize : 0,
            info.IsReady ? info.TotalFreeSpace : 0,
            format,
            string.Equals(format, "NTFS", StringComparison.OrdinalIgnoreCase));
    }

    public IEnumerable<string> EnumerateReparsePoints(string root, CancellationToken ct)
    {
        var stack = new Stack<string>();
        stack.Push(root);

        while (stack.Count > 0)
        {
            ct.ThrowIfCancellationRequested();
            var current = stack.Pop();

            IEnumerable<string> subdirs;
            try
            {
                subdirs = Directory.EnumerateDirectories(current);
            }
            catch
            {
                continue;
            }

            foreach (var dir in subdirs)
            {
                ct.ThrowIfCancellationRequested();

                FileAttributes attributes;
                try
                {
                    attributes = File.GetAttributes(dir);
                }
                catch
                {
                    continue;
                }

                if ((attributes & FileAttributes.ReparsePoint) != 0)
                {
                    yield return dir;
                }
                else
                {
                    stack.Push(dir);
                }
            }
        }
    }

    private static long SumSize(string path, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        long total = 0;
        long itemCount = 0;

        foreach (var file in Directory.EnumerateFiles(path, "*", options))
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                total += new FileInfo(file).Length;
                itemCount++;
                if (itemCount % 500 == 0)
                {
                    progress?.Report(new ScanProgress(file, total, itemCount));
                }
            }
            catch
            {
                // skip files we cannot stat
            }
        }

        progress?.Report(new ScanProgress(path, total, itemCount));
        return total;
    }

    private static long CountFiles(string path, CancellationToken ct)
    {
        var options = new EnumerationOptions
        {
            IgnoreInaccessible = true,
            RecurseSubdirectories = true,
            AttributesToSkip = FileAttributes.ReparsePoint
        };

        long count = 0;
        foreach (var _ in Directory.EnumerateFiles(path, "*", options))
        {
            ct.ThrowIfCancellationRequested();
            count++;
        }
        return count;
    }
}
