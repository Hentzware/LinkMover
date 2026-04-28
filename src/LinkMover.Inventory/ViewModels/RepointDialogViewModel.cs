// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.IO;
using LinkMover.Inventory.Repoint;
using LinkMover.Inventory.Restore;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;

namespace LinkMover.Inventory.ViewModels;

public class RepointDialogViewModel : BindableBase
{
    private readonly IJunctionRepointOrchestrator _orchestrator;
    private CancellationTokenSource? _cts;
    private DialogPhase _phase;
    private string _newTarget;
    private bool _deleteOldTargetAfter = true;
    private string _stageLabel = string.Empty;
    private string _currentFile = string.Empty;
    private string _progressDetail = string.Empty;
    private long _bytesProcessed;
    private long _totalBytes;
    private RepointResult? _result;

    public RepointDialogViewModel(IJunctionRepointOrchestrator orchestrator, string source, string oldTarget)
    {
        _orchestrator = orchestrator;
        Source = source;
        OldTarget = oldTarget;
        _newTarget = SuggestNewTarget(oldTarget);
        Phase = DialogPhase.Confirm;

        StartCommand = new DelegateCommand(async () => await StartAsync(), CanStart);
        CancelCommand = new DelegateCommand(Cancel);
        CloseCommand = new DelegateCommand(() => RequestClose?.Invoke(Result?.Success == true));
        BrowseCommand = new DelegateCommand(Browse);
    }

    public event Action<bool>? RequestClose;

    public string Source { get; }
    public string OldTarget { get; }

    public string NewTarget
    {
        get => _newTarget;
        set
        {
            if (SetProperty(ref _newTarget, value))
            {
                StartCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public bool DeleteOldTargetAfter
    {
        get => _deleteOldTargetAfter;
        set => SetProperty(ref _deleteOldTargetAfter, value);
    }

    public DialogPhase Phase
    {
        get => _phase;
        set
        {
            if (SetProperty(ref _phase, value))
            {
                RaisePropertyChanged(nameof(ShowConfirm));
                RaisePropertyChanged(nameof(ShowProgress));
                RaisePropertyChanged(nameof(ShowResult));
                RaisePropertyChanged(nameof(ShowStartButton));
                RaisePropertyChanged(nameof(ShowCancelButton));
                RaisePropertyChanged(nameof(ShowCloseButton));
            }
        }
    }

    public bool ShowConfirm => Phase == DialogPhase.Confirm;
    public bool ShowProgress => Phase == DialogPhase.Progress;
    public bool ShowResult => Phase == DialogPhase.Result;

    public bool ShowStartButton => Phase == DialogPhase.Confirm;
    public bool ShowCancelButton => Phase == DialogPhase.Confirm || Phase == DialogPhase.Progress;
    public bool ShowCloseButton => Phase == DialogPhase.Result;

    public string StageLabel { get => _stageLabel; set => SetProperty(ref _stageLabel, value); }
    public string CurrentFile { get => _currentFile; set => SetProperty(ref _currentFile, value); }
    public string ProgressDetail { get => _progressDetail; set => SetProperty(ref _progressDetail, value); }

    public long BytesProcessed { get => _bytesProcessed; set => SetProperty(ref _bytesProcessed, value); }

    public long TotalBytes
    {
        get => _totalBytes;
        set
        {
            if (SetProperty(ref _totalBytes, value))
            {
                RaisePropertyChanged(nameof(IsIndeterminate));
            }
        }
    }

    public bool IsIndeterminate => TotalBytes <= 0;

    public RepointResult? Result
    {
        get => _result;
        set
        {
            if (SetProperty(ref _result, value))
            {
                RaisePropertyChanged(nameof(ResultHeadline));
                RaisePropertyChanged(nameof(ResultDetail));
                RaisePropertyChanged(nameof(IsResultSuccess));
            }
        }
    }

    public bool IsResultSuccess => Result?.Success == true;

    public string ResultHeadline => Result is null
        ? string.Empty
        : Result.Success ? "Ziel-Anpassung erfolgreich." : "Ziel-Anpassung fehlgeschlagen.";

    public string ResultDetail
    {
        get
        {
            if (Result is null) return string.Empty;
            if (!Result.Success) return Result.ErrorMessage ?? "Unbekannter Fehler.";
            var summary = $"{Result.FilesCopied} Dateien, {Format(Result.BytesCopied)} an neues Ziel verschoben.";
            if (Result.WarningMessage is not null) summary += "\n\n" + Result.WarningMessage;
            return summary;
        }
    }

    public DelegateCommand StartCommand { get; }
    public DelegateCommand CancelCommand { get; }
    public DelegateCommand CloseCommand { get; }
    public DelegateCommand BrowseCommand { get; }

    private bool CanStart() => !string.IsNullOrWhiteSpace(NewTarget);

    private async Task StartAsync()
    {
        Phase = DialogPhase.Progress;
        _cts = new CancellationTokenSource();

        var progress = new Progress<RepointProgress>(p =>
        {
            StageLabel = StageToLabel(p.Stage);
            BytesProcessed = p.BytesProcessed;
            TotalBytes = p.TotalBytes;
            CurrentFile = p.CurrentFile;
            ProgressDetail = p.TotalBytes > 0
                ? $"{Format(p.BytesProcessed)} / {Format(p.TotalBytes)}"
                : Format(p.BytesProcessed);
        });

        try
        {
            Result = await _orchestrator.RepointAsync(Source, OldTarget, NewTarget,
                DeleteOldTargetAfter, progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            Result = new RepointResult(false, "Abgebrochen.", null, 0, 0, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            Result = new RepointResult(false, ex.Message, null, 0, 0, TimeSpan.Zero);
        }

        Phase = DialogPhase.Result;
    }

    private void Cancel()
    {
        if (Phase == DialogPhase.Confirm)
        {
            RequestClose?.Invoke(false);
        }
        else
        {
            _cts?.Cancel();
        }
    }

    private void Browse()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Neues Ziel-Verzeichnis wählen",
            InitialDirectory = Directory.Exists(Path.GetDirectoryName(NewTarget))
                ? Path.GetDirectoryName(NewTarget)!
                : Environment.GetFolderPath(Environment.SpecialFolder.MyComputer)
        };
        if (dialog.ShowDialog() == true)
        {
            NewTarget = Path.Combine(dialog.FolderName, Path.GetFileName(OldTarget));
        }
    }

    private static string SuggestNewTarget(string oldTarget)
    {
        // Suggest the same leaf name on a different drive if available, else just append "-new".
        var leaf = Path.GetFileName(oldTarget);
        if (string.IsNullOrEmpty(leaf)) leaf = "data";

        var oldRoot = Path.GetPathRoot(oldTarget) ?? "C:\\";
        var alternative = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Where(d => !string.Equals(d.RootDirectory.FullName, oldRoot, StringComparison.OrdinalIgnoreCase))
            .Select(d => d.RootDirectory.FullName)
            .FirstOrDefault();

        return alternative is not null
            ? Path.Combine(alternative, "AppData", leaf)
            : oldTarget + "-new";
    }

    private static string StageToLabel(RepointStage stage) => stage switch
    {
        RepointStage.PreFlight => "Prüfe Pfade…",
        RepointStage.Copying => "Daten werden auf neues Ziel kopiert…",
        RepointStage.Verifying => "Verifiziere Kopie…",
        RepointStage.RemovingOldJunction => "Entferne alte Junction…",
        RepointStage.CreatingNewJunction => "Erstelle neue Junction…",
        RepointStage.DeletingOldTarget => "Lösche altes Ziel…",
        RepointStage.Done => "Fertig.",
        _ => stage.ToString()
    };

    private static string Format(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
