// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Diagnostics;

namespace LinkMover.Core.Tests;

public sealed class TempDirectoryFixture : IDisposable
{
    public string Root { get; }

    public TempDirectoryFixture()
    {
        Root = Path.Combine(Path.GetTempPath(), "LinkMover.Tests-" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(Root);
    }

    public string CreateSubDir(string name)
    {
        var path = Path.Combine(Root, name);
        Directory.CreateDirectory(path);
        return path;
    }

    public string CreateFile(string relativePath, string content = "x")
    {
        var path = Path.Combine(Root, relativePath);
        Directory.CreateDirectory(Path.GetDirectoryName(path)!);
        File.WriteAllText(path, content);
        return path;
    }

    public string CreateJunction(string source, string target)
    {
        var psi = new ProcessStartInfo("cmd.exe", $"/c mklink /J \"{source}\" \"{target}\"")
        {
            CreateNoWindow = true,
            UseShellExecute = false,
            RedirectStandardOutput = true,
            RedirectStandardError = true
        };
        using var proc = Process.Start(psi)!;
        proc.WaitForExit();
        if (proc.ExitCode != 0)
        {
            throw new InvalidOperationException($"mklink fehlgeschlagen: {proc.StandardError.ReadToEnd()}");
        }
        return source;
    }

    public void Dispose()
    {
        try
        {
            if (Directory.Exists(Root))
            {
                Directory.Delete(Root, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }
}
