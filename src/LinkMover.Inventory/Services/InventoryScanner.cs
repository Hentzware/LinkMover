// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Abstractions;
using LinkMover.Inventory.Models;

namespace LinkMover.Inventory.Services;

public sealed class InventoryScanner : IInventoryScanner
{
    private readonly IFileSystemService _fileSystem;
    private readonly IJunctionService _junctionService;

    public InventoryScanner(IFileSystemService fileSystem, IJunctionService junctionService)
    {
        _fileSystem = fileSystem;
        _junctionService = junctionService;
    }

    public Task ScanAsync(
        IEnumerable<string> roots,
        IProgress<InventoryEntry>? onEntry,
        IProgress<string>? onCurrentPath,
        CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var seen = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            foreach (var root in roots)
            {
                ct.ThrowIfCancellationRequested();
                if (!_fileSystem.DirectoryExists(root))
                {
                    continue;
                }

                onCurrentPath?.Report(root);

                foreach (var path in _fileSystem.EnumerateReparsePoints(root, ct))
                {
                    ct.ThrowIfCancellationRequested();
                    if (!seen.Add(path))
                    {
                        continue;
                    }

                    onCurrentPath?.Report(path);

                    var info = _junctionService.Inspect(path);
                    if (info is null)
                    {
                        continue;
                    }

                    onEntry?.Report(new InventoryEntry(
                        sourcePath: info.Source,
                        targetPath: info.Target,
                        created: info.Created,
                        targetExists: info.TargetExists));
                }
            }
        }, ct);
    }
}

public static class InventoryScannerDefaults
{
    public static IReadOnlyList<string> StandardRoots { get; } = new[]
    {
        Environment.GetFolderPath(Environment.SpecialFolder.UserProfile),
        Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData),
        Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
    };
}
