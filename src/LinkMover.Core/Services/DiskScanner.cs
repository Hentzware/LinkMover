// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Abstractions;
using LinkMover.Core.Models;

namespace LinkMover.Core.Services;

public sealed class DiskScanner : IDiskScanner
{
    public Task<DirectoryNode> ScanRecursiveAsync(string root, IProgress<ScanProgress>? progress, CancellationToken ct)
    {
        return Task.Run(() => Scan(root, progress, ct, totalCounter: new long[1]), ct);
    }

    private static DirectoryNode Scan(
        string path,
        IProgress<ScanProgress>? progress,
        CancellationToken ct,
        long[] totalCounter)
    {
        ct.ThrowIfCancellationRequested();

        var children = new List<DirectoryNode>();
        var files = new List<FileNode>();
        long sizeBytes = 0;
        long fileCount = 0;

        // Files in this directory.
        try
        {
            foreach (var file in Directory.EnumerateFiles(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var info = new FileInfo(file);
                    files.Add(new FileNode(info.Name, info.Length, info.Extension));
                    sizeBytes += info.Length;
                    fileCount++;

                    totalCounter[0]++;
                    if (totalCounter[0] % 1_000 == 0)
                    {
                        progress?.Report(new ScanProgress(file, sizeBytes, totalCounter[0]));
                    }
                }
                catch
                {
                    // Skip unreadable files (locked, ACL, sparse weirdness).
                }
            }
        }
        catch
        {
            // Skip unreadable directory enumeration.
        }

        // Subdirectories — descend, but never into reparse points (junctions/symlinks).
        try
        {
            foreach (var subdir in Directory.EnumerateDirectories(path))
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    var attrs = File.GetAttributes(subdir);
                    if ((attrs & FileAttributes.ReparsePoint) != 0)
                    {
                        continue;
                    }

                    var child = Scan(subdir, progress, ct, totalCounter);
                    children.Add(child);
                    sizeBytes += child.SizeBytes;
                    fileCount += child.FileCount;
                }
                catch
                {
                    // Skip inaccessible subdirectories.
                }
            }
        }
        catch
        {
            // Skip unreadable directory enumeration.
        }

        return new DirectoryNode(path, sizeBytes, fileCount, children, files);
    }
}
