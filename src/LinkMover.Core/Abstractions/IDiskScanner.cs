// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Models;

namespace LinkMover.Core.Abstractions;

public interface IDiskScanner
{
    Task<DirectoryNode> ScanRecursiveAsync(string root, IProgress<ScanProgress>? progress, CancellationToken ct);
}
