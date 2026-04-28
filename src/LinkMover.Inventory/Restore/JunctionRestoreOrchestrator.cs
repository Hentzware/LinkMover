// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Diagnostics;
using System.IO;
using LinkMover.Core.Abstractions;
using LinkMover.Core.Models;

namespace LinkMover.Inventory.Restore;

public sealed class JunctionRestoreOrchestrator : IJunctionRestoreOrchestrator
{
    private readonly IFileSystemService _fileSystem;
    private readonly IJunctionService _junctionService;
    private readonly IRobocopyService _robocopyService;
    private readonly IVerificationService _verificationService;

    public JunctionRestoreOrchestrator(
        IFileSystemService fileSystem,
        IJunctionService junctionService,
        IRobocopyService robocopyService,
        IVerificationService verificationService)
    {
        _fileSystem = fileSystem;
        _junctionService = junctionService;
        _robocopyService = robocopyService;
        _verificationService = verificationService;
    }

    public async Task<RestoreResult> RestoreAsync(
        string source,
        string target,
        bool deleteTargetAfter,
        IProgress<RestoreProgress>? progress,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        // Pre-flight
        progress?.Report(new RestoreProgress(RestoreStage.PreFlight, "Prüfe Pfade…", 0, 0, string.Empty));

        if (!_fileSystem.IsReparsePoint(source))
        {
            return Failed("Quelle ist kein Reparse-Point.", stopwatch);
        }
        if (!_fileSystem.DirectoryExists(target))
        {
            return Failed($"Ziel-Verzeichnis fehlt: {target}", stopwatch);
        }

        var stagingPath = source + ".lm-restore-" + Guid.NewGuid().ToString("N").Substring(0, 8);
        if (Directory.Exists(stagingPath))
        {
            try { Directory.Delete(stagingPath, recursive: true); }
            catch (Exception ex) { return Failed($"Veraltetes Staging-Verzeichnis nicht löschbar: {ex.Message}", stopwatch); }
        }

        var totalBytes = await _fileSystem.GetDirectorySizeAsync(target, progress: null, ct);

        // Stage: Robocopy target → staging
        progress?.Report(new RestoreProgress(RestoreStage.Staging, "Daten werden zurückkopiert…", 0, totalBytes, string.Empty));

        var copyProgress = new Progress<RobocopyProgress>(rp =>
            progress?.Report(new RestoreProgress(RestoreStage.Staging, "Daten werden zurückkopiert…", rp.TotalBytesCopied, totalBytes, rp.CurrentFile)));

        RobocopyResult copyResult;
        try
        {
            copyResult = await _robocopyService.CopyAsync(target, stagingPath, new RobocopyOptions(Mirror: true), copyProgress, ct);
        }
        catch (OperationCanceledException)
        {
            CleanupStaging(stagingPath);
            throw;
        }

        if (!copyResult.IsSuccess)
        {
            CleanupStaging(stagingPath);
            return Failed($"Robocopy fehlgeschlagen mit ExitCode {copyResult.ExitCode}.", stopwatch);
        }

        // Verify
        progress?.Report(new RestoreProgress(RestoreStage.Verifying, "Verifiziere Kopie…", totalBytes, totalBytes, string.Empty));
        var verify = await _verificationService.VerifyAsync(target, stagingPath, VerifyMode.FileCountAndSize, ct);
        if (!verify.Match)
        {
            CleanupStaging(stagingPath);
            return Failed(
                $"Verifikation fehlgeschlagen: {verify.SourceFileCount} Dateien / {verify.SourceBytes} B in Ziel, {verify.TargetFileCount} / {verify.TargetBytes} B im Staging.",
                stopwatch);
        }

        // Remove junction (source path becomes available for the staged data)
        progress?.Report(new RestoreProgress(RestoreStage.RemovingJunction, "Entferne Junction…", totalBytes, totalBytes, string.Empty));
        try
        {
            await _junctionService.RemoveJunctionAsync(source);
        }
        catch (Exception ex)
        {
            CleanupStaging(stagingPath);
            return Failed($"Junction konnte nicht entfernt werden: {ex.Message}", stopwatch);
        }

        // Move staging → source (rename, atomic on same volume)
        progress?.Report(new RestoreProgress(RestoreStage.MovingStaging, "Verschiebe Daten an Originalort…", totalBytes, totalBytes, string.Empty));
        try
        {
            Directory.Move(stagingPath, source);
        }
        catch (Exception ex)
        {
            // Worst case: junction is gone but staging is still at the temp path. Surface clearly.
            return Failed(
                $"Junction wurde entfernt, aber Staging konnte nicht an den Originalort verschoben werden ({ex.Message}). " +
                $"Daten liegen noch unter: {stagingPath}",
                stopwatch);
        }

        // Optional: delete original target
        string? warning = null;
        if (deleteTargetAfter)
        {
            progress?.Report(new RestoreProgress(RestoreStage.DeletingTarget, "Lösche Original-Ziel…", totalBytes, totalBytes, string.Empty));
            try
            {
                Directory.Delete(target, recursive: true);
            }
            catch (Exception ex)
            {
                warning = $"Wiederherstellung erfolgreich, Ziel konnte aber nicht gelöscht werden: {ex.Message}";
            }
        }

        progress?.Report(new RestoreProgress(RestoreStage.Done, "Fertig.", totalBytes, totalBytes, string.Empty));

        return new RestoreResult(
            Success: true,
            ErrorMessage: null,
            WarningMessage: warning,
            FilesRestored: verify.SourceFileCount,
            BytesRestored: verify.SourceBytes,
            Elapsed: stopwatch.Elapsed);
    }

    private static void CleanupStaging(string stagingPath)
    {
        try
        {
            if (Directory.Exists(stagingPath))
            {
                Directory.Delete(stagingPath, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static RestoreResult Failed(string error, Stopwatch sw)
        => new(Success: false, ErrorMessage: error, WarningMessage: null,
            FilesRestored: 0, BytesRestored: 0, Elapsed: sw.Elapsed);
}
