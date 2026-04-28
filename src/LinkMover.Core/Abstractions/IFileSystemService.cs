// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Models;

namespace LinkMover.Core.Abstractions;

public interface IFileSystemService
{
    bool DirectoryExists(string path);

    bool IsReparsePoint(string path);

    string? ResolveLinkTarget(string path);

    Task<long> GetDirectorySizeAsync(string path, IProgress<ScanProgress>? progress, CancellationToken ct);

    Task<long> CountFilesAsync(string path, CancellationToken ct);

    DriveInfoSnapshot GetDrive(string path);

    IEnumerable<string> EnumerateReparsePoints(string root, CancellationToken ct);
}
