// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Inventory.Models;

namespace LinkMover.Inventory.Services;

public interface IInventoryScanner
{
    // Streams entries via onEntry as they're discovered so the UI can populate
    // the grid live. onCurrentPath reports the directory currently being walked
    // so the status banner shows what the scanner is on right now.
    Task ScanAsync(
        IEnumerable<string> roots,
        IProgress<InventoryEntry>? onEntry,
        IProgress<string>? onCurrentPath,
        CancellationToken ct);
}
