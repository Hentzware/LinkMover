// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using Prism.Mvvm;

namespace LinkMover.Inventory.Models;

public sealed class InventoryEntry : BindableBase
{
    private long? _sizeBytes;

    public InventoryEntry(string sourcePath, string targetPath, DateTime created, bool targetExists)
    {
        SourcePath = sourcePath;
        TargetPath = targetPath;
        Created = created;
        TargetExists = targetExists;
    }

    public string SourcePath { get; }
    public string TargetPath { get; }
    public DateTime Created { get; }
    public bool TargetExists { get; }

    public long? SizeBytes
    {
        get => _sizeBytes;
        set
        {
            if (SetProperty(ref _sizeBytes, value))
            {
                RaisePropertyChanged(nameof(SizeText));
            }
        }
    }

    public string SizeText => SizeBytes is null ? "…" : FormatBytes(SizeBytes.Value);

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
