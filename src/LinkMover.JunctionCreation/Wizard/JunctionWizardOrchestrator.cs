// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.IO;
using LinkMover.Core.Abstractions;
using LinkMover.Core.Models;

namespace LinkMover.JunctionCreation.Wizard;

public sealed class JunctionWizardOrchestrator : IJunctionWizardOrchestrator
{
    private readonly IFileSystemService _fileSystem;
    private readonly IJunctionService _junctionService;
    private readonly IRobocopyService _robocopyService;
    private readonly IVerificationService _verificationService;
    private readonly IRestartManagerService _restartManager;
    private readonly IPrivilegeService _privilegeService;

    public WizardContext Context { get; } = new();

    public JunctionWizardOrchestrator(
        IFileSystemService fileSystem,
        IJunctionService junctionService,
        IRobocopyService robocopyService,
        IVerificationService verificationService,
        IRestartManagerService restartManager,
        IPrivilegeService privilegeService)
    {
        _fileSystem = fileSystem;
        _junctionService = junctionService;
        _robocopyService = robocopyService;
        _verificationService = verificationService;
        _restartManager = restartManager;
        _privilegeService = privilegeService;
    }

    public void Reset() => Context.Reset();

    public async Task RunPreFlightAsync(CancellationToken ct)
    {
        var checks = new List<PreFlightCheck>();

        if (!_fileSystem.DirectoryExists(Context.Source))
        {
            checks.Add(new PreFlightCheck("Quelle", Severity.Error, "Quelle existiert nicht."));
        }
        else if (_fileSystem.IsReparsePoint(Context.Source))
        {
            checks.Add(new PreFlightCheck("Quelle", Severity.Error, "Quelle ist bereits ein Reparse-Point (Junction/Symlink)."));
        }
        else
        {
            checks.Add(new PreFlightCheck("Quelle", Severity.Ok, "Quelle ist ein normales Verzeichnis."));
        }

        if (_fileSystem.DirectoryExists(Context.Target))
        {
            try
            {
                if (Directory.EnumerateFileSystemEntries(Context.Target).Any())
                {
                    checks.Add(new PreFlightCheck("Ziel", Severity.Error, "Ziel existiert und ist nicht leer."));
                }
                else
                {
                    checks.Add(new PreFlightCheck("Ziel", Severity.Warning, "Ziel existiert bereits, ist aber leer."));
                }
            }
            catch (Exception ex)
            {
                checks.Add(new PreFlightCheck("Ziel", Severity.Error, $"Ziel-Verzeichnis nicht zugreifbar: {ex.Message}"));
            }
        }
        else
        {
            checks.Add(new PreFlightCheck("Ziel", Severity.Ok, "Ziel existiert nicht und wird beim Kopieren erstellt."));
        }

        try
        {
            var sourceSize = await _fileSystem.GetDirectorySizeAsync(Context.Source, progress: null, ct);
            Context.SourceSizeBytes = sourceSize;
            var targetDrive = _fileSystem.GetDrive(Context.Target);
            var requiredWithMargin = (long)(sourceSize * 1.1);
            if (targetDrive.FreeBytes < requiredWithMargin)
            {
                checks.Add(new PreFlightCheck("Speicherplatz", Severity.Error,
                    $"Zielvolume {targetDrive.Root} hat {Format(targetDrive.FreeBytes)} frei, Quelle benötigt {Format(sourceSize)} (+10 % Reserve)."));
            }
            else
            {
                checks.Add(new PreFlightCheck("Speicherplatz", Severity.Ok,
                    $"Quelle {Format(sourceSize)}, frei auf Zielvolume {Format(targetDrive.FreeBytes)}."));
            }
        }
        catch (Exception ex)
        {
            checks.Add(new PreFlightCheck("Speicherplatz", Severity.Warning, $"Größe konnte nicht ermittelt werden: {ex.Message}"));
        }

        var sourceParent = Path.GetDirectoryName(Context.Source);
        if (string.IsNullOrEmpty(sourceParent))
        {
            checks.Add(new PreFlightCheck("Schreibrechte", Severity.Error, "Quell-Pfad hat kein Eltern-Verzeichnis."));
        }
        else if (!_privilegeService.CanWriteTo(sourceParent))
        {
            checks.Add(new PreFlightCheck("Schreibrechte", Severity.Error,
                $"Keine Schreibrechte auf {sourceParent}. Junction-Erstellung schlägt fehl. Eskalation als Administrator nötig."));
        }
        else
        {
            checks.Add(new PreFlightCheck("Schreibrechte", Severity.Ok, $"Schreibrechte auf {sourceParent} vorhanden."));
        }

        try
        {
            var sample = Directory.EnumerateFiles(Context.Source, "*", SearchOption.TopDirectoryOnly).Take(20).ToList();
            if (sample.Count > 0)
            {
                var locks = await _restartManager.GetLockingProcessesAsync(sample, ct);
                Context.LockingProcesses = locks;
                if (locks.Count > 0)
                {
                    var sampleNames = string.Join(", ", locks.Take(3).Select(p => p.ProcessName));
                    checks.Add(new PreFlightCheck("Offene Handles", Severity.Warning,
                        $"{locks.Count} Prozess(e) halten Handles auf Quell-Dateien: {sampleNames}"));
                }
                else
                {
                    checks.Add(new PreFlightCheck("Offene Handles", Severity.Ok, "Keine sperrenden Prozesse erkannt."));
                }
            }
        }
        catch (Exception ex)
        {
            checks.Add(new PreFlightCheck("Offene Handles", Severity.Warning, $"Lock-Prüfung fehlgeschlagen: {ex.Message}"));
        }

        Context.PreFlight = new PreFlightReport(checks);
    }

    public async Task RunCopyAsync(IProgress<RobocopyProgress>? progress, CancellationToken ct)
    {
        var options = new RobocopyOptions(Mirror: true, DryRun: Context.DryRun);
        Context.CopyResult = await _robocopyService.CopyAsync(Context.Source, Context.Target, options, progress, ct);
    }

    public async Task RunVerifyAsync(CancellationToken ct)
    {
        if (Context.DryRun)
        {
            Context.VerifyResult = new VerifyResult(true, 0, 0, 0, 0, Array.Empty<string>());
            return;
        }
        Context.VerifyResult = await _verificationService.VerifyAsync(
            Context.Source, Context.Target, VerifyMode.FileCountAndSize, ct);
    }

    public Task RunCleanupAsync(CancellationToken ct)
    {
        if (Context.DryRun)
        {
            Context.CleanupDone = true;
            return Task.CompletedTask;
        }

        // Hard gate: never delete the source unless verify said the copy is sound.
        if (Context.VerifyResult is null || !Context.VerifyResult.Match)
        {
            throw new InvalidOperationException("Cleanup darf nicht ohne erfolgreiche Verifikation aufgerufen werden.");
        }

        return Task.Run(() =>
        {
            ct.ThrowIfCancellationRequested();
            Directory.Delete(Context.Source, recursive: true);
            Context.CleanupDone = true;
        }, ct);
    }

    public async Task RunCreateLinkAsync(CancellationToken ct)
    {
        if (Context.DryRun)
        {
            Context.LinkCreated = true;
            return;
        }

        var success = await _junctionService.CreateJunctionAsync(Context.Source, Context.Target, ct);
        if (!success)
        {
            throw new InvalidOperationException("Junction-Erstellung (mklink /J) ist fehlgeschlagen.");
        }
        Context.LinkCreated = true;
        Context.JunctionInfo = _junctionService.Inspect(Context.Source);
    }

    private static string Format(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
