// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

namespace LinkMover.Inventory.Repoint;

public interface IJunctionRepointOrchestrator
{
    Task<RepointResult> RepointAsync(
        string source,
        string oldTarget,
        string newTarget,
        bool deleteOldTargetAfter,
        IProgress<RepointProgress>? progress,
        CancellationToken ct);
}
