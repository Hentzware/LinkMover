// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Diagnostics;
using LinkMover.Core.Abstractions;
using LinkMover.Core.Models;

namespace LinkMover.Core.Services;

public sealed class JunctionService : IJunctionService
{
    private readonly IFileSystemService _fileSystem;

    public JunctionService(IFileSystemService fileSystem)
    {
        _fileSystem = fileSystem;
    }

    public async Task<bool> CreateJunctionAsync(string source, string target, CancellationToken ct)
    {
        if (Directory.Exists(source))
        {
            throw new InvalidOperationException($"Quelle existiert bereits: {source}");
        }

        var targetFull = Path.GetFullPath(target);
        if (!Directory.Exists(targetFull))
        {
            throw new InvalidOperationException($"Ziel existiert nicht: {targetFull}");
        }

        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{source}\" \"{targetFull}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };

        using var proc = Process.Start(psi)
                         ?? throw new InvalidOperationException("cmd.exe konnte nicht gestartet werden.");
        await proc.WaitForExitAsync(ct);
        return proc.ExitCode == 0 && _fileSystem.IsReparsePoint(source);
    }

    public Task<bool> RemoveJunctionAsync(string source)
    {
        if (!Directory.Exists(source))
        {
            return Task.FromResult(false);
        }

        if (!_fileSystem.IsReparsePoint(source))
        {
            throw new InvalidOperationException($"Pfad ist kein Reparse-Point: {source}");
        }

        Directory.Delete(source);
        return Task.FromResult(true);
    }

    public JunctionInfo? Inspect(string path)
    {
        if (!Directory.Exists(path) || !_fileSystem.IsReparsePoint(path))
        {
            return null;
        }

        var target = _fileSystem.ResolveLinkTarget(path);
        if (target is null)
        {
            return null;
        }

        var info = new DirectoryInfo(path);
        return new JunctionInfo(
            Source: Path.GetFullPath(path),
            Target: target,
            TargetSizeBytes: null,
            Created: info.CreationTimeUtc,
            TargetExists: Directory.Exists(target));
    }
}
