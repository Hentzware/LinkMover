// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Diagnostics;
using System.Security.Principal;
using LinkMover.Core.Abstractions;

namespace LinkMover.Core.Services;

public sealed class PrivilegeService : IPrivilegeService
{
    public bool IsElevated
    {
        get
        {
            using var identity = WindowsIdentity.GetCurrent();
            var principal = new WindowsPrincipal(identity);
            return principal.IsInRole(WindowsBuiltInRole.Administrator);
        }
    }

    public bool CanWriteTo(string path)
    {
        if (!Directory.Exists(path))
        {
            return false;
        }

        var probe = Path.Combine(path, $".lm-write-probe-{Guid.NewGuid():N}");
        try
        {
            using (File.Create(probe)) { }
            File.Delete(probe);
            return true;
        }
        catch
        {
            return false;
        }
    }

    public Task<bool> RestartElevatedAsync(string operationToResume)
    {
        try
        {
            var exe = Environment.ProcessPath
                      ?? throw new InvalidOperationException("Eigener Prozesspfad ist nicht verfügbar.");

            var psi = new ProcessStartInfo(exe)
            {
                Verb = "runas",
                UseShellExecute = true,
                Arguments = $"--resume \"{operationToResume}\""
            };

            using var proc = Process.Start(psi);
            return Task.FromResult(proc is not null);
        }
        catch
        {
            return Task.FromResult(false);
        }
    }
}
