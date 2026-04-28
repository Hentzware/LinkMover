// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Runtime.InteropServices.ComTypes;
using LinkMover.Core.Abstractions;
using LinkMover.Core.Models;

namespace LinkMover.Core.Services;

public sealed class RestartManagerService : IRestartManagerService
{
    private const int CCH_RM_MAX_APP_NAME = 255;
    private const int CCH_RM_MAX_SVC_NAME = 63;
    private const int ERROR_SUCCESS = 0;
    private const int ERROR_MORE_DATA = 234;

    public Task<IReadOnlyList<LockingProcessInfo>> GetLockingProcessesAsync(IEnumerable<string> paths, CancellationToken ct)
    {
        return Task.Run<IReadOnlyList<LockingProcessInfo>>(() => GetLocking(paths, ct), ct);
    }

    public Task<bool> EndProcessesAsync(IEnumerable<int> processIds, CancellationToken ct)
    {
        return Task.Run(() =>
        {
            var allEnded = true;
            foreach (var pid in processIds)
            {
                ct.ThrowIfCancellationRequested();
                try
                {
                    using var proc = Process.GetProcessById(pid);
                    proc.Kill(entireProcessTree: true);
                    proc.WaitForExit(5_000);
                }
                catch
                {
                    allEnded = false;
                }
            }
            return allEnded;
        }, ct);
    }

    private static IReadOnlyList<LockingProcessInfo> GetLocking(IEnumerable<string> paths, CancellationToken ct)
    {
        var files = paths.ToArray();
        if (files.Length == 0)
        {
            return Array.Empty<LockingProcessInfo>();
        }

        var sessionKey = Guid.NewGuid().ToString();

        var rv = RmStartSession(out var sessionHandle, 0, sessionKey);
        if (rv != ERROR_SUCCESS)
        {
            throw new InvalidOperationException($"RmStartSession ist fehlgeschlagen: Win32-Code {rv}.");
        }

        try
        {
            ct.ThrowIfCancellationRequested();

            rv = RmRegisterResources(sessionHandle, (uint)files.Length, files, 0, null, 0, null);
            if (rv != ERROR_SUCCESS)
            {
                throw new InvalidOperationException($"RmRegisterResources ist fehlgeschlagen: Win32-Code {rv}.");
            }

            uint procInfoNeeded = 0;
            uint procInfo = 0;
            uint rebootReasons = 0;

            rv = RmGetList(sessionHandle, out procInfoNeeded, ref procInfo, null, ref rebootReasons);
            if (rv == ERROR_SUCCESS || procInfoNeeded == 0)
            {
                return Array.Empty<LockingProcessInfo>();
            }
            if (rv != ERROR_MORE_DATA)
            {
                throw new InvalidOperationException($"RmGetList (probe) ist fehlgeschlagen: Win32-Code {rv}.");
            }

            procInfo = procInfoNeeded;
            var buffer = new RM_PROCESS_INFO[procInfoNeeded];
            rv = RmGetList(sessionHandle, out procInfoNeeded, ref procInfo, buffer, ref rebootReasons);
            if (rv != ERROR_SUCCESS)
            {
                throw new InvalidOperationException($"RmGetList ist fehlgeschlagen: Win32-Code {rv}.");
            }

            var result = new List<LockingProcessInfo>((int)procInfo);
            for (var i = 0; i < procInfo; i++)
            {
                ct.ThrowIfCancellationRequested();
                var entry = buffer[i];
                var exePath = string.Empty;
                try
                {
                    using var process = Process.GetProcessById(entry.Process.dwProcessId);
                    exePath = process.MainModule?.FileName ?? string.Empty;
                }
                catch
                {
                    // Process exited, or access denied for system processes — leave empty.
                }

                result.Add(new LockingProcessInfo(
                    entry.Process.dwProcessId,
                    entry.strAppName,
                    exePath,
                    (RestartManagerAppType)entry.ApplicationType));
            }

            return result;
        }
        finally
        {
            RmEndSession(sessionHandle);
        }
    }

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmStartSession(out uint pSessionHandle, int dwSessionFlags, string strSessionKey);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmEndSession(uint pSessionHandle);

    [DllImport("rstrtmgr.dll", CharSet = CharSet.Unicode)]
    private static extern int RmRegisterResources(
        uint pSessionHandle,
        uint nFiles,
        string[]? rgsFilenames,
        uint nApplications,
        RM_UNIQUE_PROCESS[]? rgApplications,
        uint nServices,
        string[]? rgsServiceNames);

    [DllImport("rstrtmgr.dll")]
    private static extern int RmGetList(
        uint dwSessionHandle,
        out uint pnProcInfoNeeded,
        ref uint pnProcInfo,
        [In, Out] RM_PROCESS_INFO[]? rgAffectedApps,
        ref uint lpdwRebootReasons);

    [StructLayout(LayoutKind.Sequential)]
    private struct RM_UNIQUE_PROCESS
    {
        public int dwProcessId;
        public FILETIME ProcessStartTime;
    }

    [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
    private struct RM_PROCESS_INFO
    {
        public RM_UNIQUE_PROCESS Process;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_APP_NAME + 1)]
        public string strAppName;

        [MarshalAs(UnmanagedType.ByValTStr, SizeConst = CCH_RM_MAX_SVC_NAME + 1)]
        public string strServiceShortName;

        public uint ApplicationType;
        public uint AppStatus;
        public uint TSSessionId;

        [MarshalAs(UnmanagedType.Bool)]
        public bool bRestartable;
    }
}
