// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Inventory.Models;

namespace LinkMover.Inventory.Services;

public interface IInventoryScanner
{
    Task<IReadOnlyList<InventoryEntry>> ScanAsync(IEnumerable<string> roots, CancellationToken ct);
}
