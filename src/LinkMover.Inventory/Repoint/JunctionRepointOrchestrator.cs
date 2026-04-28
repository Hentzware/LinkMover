// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Diagnostics;
using System.IO;
using LinkMover.Core.Abstractions;
using LinkMover.Core.Models;

namespace LinkMover.Inventory.Repoint;

public sealed class JunctionRepointOrchestrator : IJunctionRepointOrchestrator
{
    private readonly IFileSystemService _fileSystem;
    private readonly IJunctionService _junctionService;
    private readonly IRobocopyService _robocopyService;
    private readonly IVerificationService _verificationService;

    public JunctionRepointOrchestrator(
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

    public async Task<RepointResult> RepointAsync(
        string source,
        string oldTarget,
        string newTarget,
        bool deleteOldTargetAfter,
        IProgress<RepointProgress>? progress,
        CancellationToken ct)
    {
        var stopwatch = Stopwatch.StartNew();

        progress?.Report(new RepointProgress(RepointStage.PreFlight, "Prüfe Pfade…", 0, 0, string.Empty));

        if (!_fileSystem.IsReparsePoint(source))
        {
            return Failed("Quelle ist kein Reparse-Point.", stopwatch);
        }
        if (!_fileSystem.DirectoryExists(oldTarget))
        {
            return Failed($"Altes Ziel-Verzeichnis fehlt: {oldTarget}", stopwatch);
        }
        if (string.Equals(Path.GetFullPath(oldTarget), Path.GetFullPath(newTarget), StringComparison.OrdinalIgnoreCase))
        {
            return Failed("Neues Ziel ist mit dem alten identisch — nichts zu tun.", stopwatch);
        }
        if (_fileSystem.DirectoryExists(newTarget))
        {
            try
            {
                if (Directory.EnumerateFileSystemEntries(newTarget).Any())
                {
                    return Failed($"Neues Ziel existiert und ist nicht leer: {newTarget}", stopwatch);
                }
            }
            catch (Exception ex)
            {
                return Failed($"Neues Ziel nicht zugreifbar: {ex.Message}", stopwatch);
            }
        }

        var totalBytes = await _fileSystem.GetDirectorySizeAsync(oldTarget, progress: null, ct);

        // Copy oldTarget → newTarget
        progress?.Report(new RepointProgress(RepointStage.Copying, "Daten werden auf neues Ziel kopiert…", 0, totalBytes, string.Empty));

        var copyProgress = new Progress<RobocopyProgress>(rp =>
            progress?.Report(new RepointProgress(RepointStage.Copying, "Daten werden auf neues Ziel kopiert…", rp.TotalBytesCopied, totalBytes, rp.CurrentFile)));

        RobocopyResult copyResult;
        try
        {
            copyResult = await _robocopyService.CopyAsync(oldTarget, newTarget, new RobocopyOptions(Mirror: true), copyProgress, ct);
        }
        catch (OperationCanceledException)
        {
            CleanupNewTarget(newTarget);
            throw;
        }

        if (!copyResult.IsSuccess)
        {
            CleanupNewTarget(newTarget);
            return Failed($"Robocopy fehlgeschlagen mit ExitCode {copyResult.ExitCode}.", stopwatch);
        }

        // Verify
        progress?.Report(new RepointProgress(RepointStage.Verifying, "Verifiziere Kopie…", totalBytes, totalBytes, string.Empty));
        var verify = await _verificationService.VerifyAsync(oldTarget, newTarget, VerifyMode.FileCountAndSize, ct);
        if (!verify.Match)
        {
            CleanupNewTarget(newTarget);
            return Failed(
                $"Verifikation fehlgeschlagen: {verify.SourceFileCount} / {verify.SourceBytes} B alt vs {verify.TargetFileCount} / {verify.TargetBytes} B neu.",
                stopwatch);
        }

        // Remove old junction (source still points at oldTarget — link goes away, oldTarget data stays)
        progress?.Report(new RepointProgress(RepointStage.RemovingOldJunction, "Entferne alte Junction…", totalBytes, totalBytes, string.Empty));
        try
        {
            await _junctionService.RemoveJunctionAsync(source);
        }
        catch (Exception ex)
        {
            CleanupNewTarget(newTarget);
            return Failed($"Alte Junction konnte nicht entfernt werden: {ex.Message}", stopwatch);
        }

        // Create new junction pointing at newTarget
        progress?.Report(new RepointProgress(RepointStage.CreatingNewJunction, "Erstelle neue Junction…", totalBytes, totalBytes, string.Empty));
        try
        {
            var ok = await _junctionService.CreateJunctionAsync(source, newTarget, ct);
            if (!ok)
            {
                return Failed(
                    $"Neue Junction konnte nicht erstellt werden. Daten liegen unter {newTarget}, alte Junction wurde entfernt — bitte manuell verlinken.",
                    stopwatch);
            }
        }
        catch (Exception ex)
        {
            return Failed(
                $"Neue Junction-Erstellung fehlgeschlagen ({ex.Message}). Daten liegen unter {newTarget}, alte Junction wurde entfernt — bitte manuell verlinken.",
                stopwatch);
        }

        // Optional: delete old target
        string? warning = null;
        if (deleteOldTargetAfter)
        {
            progress?.Report(new RepointProgress(RepointStage.DeletingOldTarget, "Lösche altes Ziel…", totalBytes, totalBytes, string.Empty));
            try
            {
                Directory.Delete(oldTarget, recursive: true);
            }
            catch (Exception ex)
            {
                warning = $"Repoint erfolgreich, altes Ziel konnte aber nicht gelöscht werden: {ex.Message}";
            }
        }

        progress?.Report(new RepointProgress(RepointStage.Done, "Fertig.", totalBytes, totalBytes, string.Empty));

        return new RepointResult(
            Success: true,
            ErrorMessage: null,
            WarningMessage: warning,
            FilesCopied: verify.SourceFileCount,
            BytesCopied: verify.SourceBytes,
            Elapsed: stopwatch.Elapsed);
    }

    private static void CleanupNewTarget(string newTarget)
    {
        try
        {
            if (Directory.Exists(newTarget))
            {
                Directory.Delete(newTarget, recursive: true);
            }
        }
        catch
        {
            // best effort
        }
    }

    private static RepointResult Failed(string error, Stopwatch sw)
        => new(Success: false, ErrorMessage: error, WarningMessage: null,
            FilesCopied: 0, BytesCopied: 0, Elapsed: sw.Elapsed);
}
