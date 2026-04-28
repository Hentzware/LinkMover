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
        set => SetProperty(ref _sizeBytes, value);
    }
}
