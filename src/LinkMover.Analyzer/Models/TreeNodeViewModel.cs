// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.IO;
using LinkMover.Core.Models;

namespace LinkMover.Analyzer.Models;

public sealed class TreeNodeViewModel
{
    private const double MaxBarWidth = 200.0;

    public TreeNodeViewModel(DirectoryNode node, long rootSizeBytes)
    {
        FullPath = node.Path;
        var name = Path.GetFileName(node.Path);
        Name = string.IsNullOrEmpty(name) ? node.Path : name;
        SizeBytes = node.SizeBytes;
        FileCount = node.FileCount;
        SizeText = FormatBytes(node.SizeBytes);
        FileCountText = $"{node.FileCount:N0} Dateien";

        var ratio = rootSizeBytes > 0 ? (double)node.SizeBytes / rootSizeBytes : 0;
        BarWidth = Math.Clamp(ratio * MaxBarWidth, 0, MaxBarWidth);

        Children = node.Children
            .OrderByDescending(c => c.SizeBytes)
            .Select(c => new TreeNodeViewModel(c, rootSizeBytes))
            .ToList();
    }

    public string Name { get; }
    public string FullPath { get; }
    public long SizeBytes { get; }
    public long FileCount { get; }
    public string SizeText { get; }
    public string FileCountText { get; }
    public double BarWidth { get; }
    public IReadOnlyList<TreeNodeViewModel> Children { get; }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
