// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Inventory.Restore;
using Prism.Commands;
using Prism.Mvvm;

namespace LinkMover.Inventory.ViewModels;

public class RestoreDialogViewModel : BindableBase
{
    private readonly IJunctionRestoreOrchestrator _orchestrator;
    private CancellationTokenSource? _cts;
    private DialogPhase _phase;
    private bool _deleteTargetAfter = true;
    private string _stageLabel = string.Empty;
    private string _currentFile = string.Empty;
    private string _progressDetail = string.Empty;
    private long _bytesProcessed;
    private long _totalBytes;
    private RestoreResult? _result;

    public RestoreDialogViewModel(IJunctionRestoreOrchestrator orchestrator, string source, string target)
    {
        _orchestrator = orchestrator;
        Source = source;
        Target = target;
        Phase = DialogPhase.Confirm;

        StartCommand = new DelegateCommand(async () => await StartAsync());
        CancelCommand = new DelegateCommand(Cancel);
        CloseCommand = new DelegateCommand(() => RequestClose?.Invoke(Result?.Success == true));
    }

    public event Action<bool>? RequestClose;

    public string Source { get; }
    public string Target { get; }

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

    public bool DeleteTargetAfter
    {
        get => _deleteTargetAfter;
        set => SetProperty(ref _deleteTargetAfter, value);
    }

    public string StageLabel { get => _stageLabel; set => SetProperty(ref _stageLabel, value); }
    public string CurrentFile { get => _currentFile; set => SetProperty(ref _currentFile, value); }
    public string ProgressDetail { get => _progressDetail; set => SetProperty(ref _progressDetail, value); }

    public long BytesProcessed
    {
        get => _bytesProcessed;
        set => SetProperty(ref _bytesProcessed, value);
    }

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

    public RestoreResult? Result
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

    public string ResultHeadline
    {
        get
        {
            if (Result is null) return string.Empty;
            return Result.Success
                ? "Wiederherstellung erfolgreich."
                : "Wiederherstellung fehlgeschlagen.";
        }
    }

    public string ResultDetail
    {
        get
        {
            if (Result is null) return string.Empty;
            if (!Result.Success) return Result.ErrorMessage ?? "Unbekannter Fehler.";
            var summary = $"{Result.FilesRestored} Dateien, {Format(Result.BytesRestored)} an Originalort zurückgeholt.";
            if (Result.WarningMessage is not null) summary += "\n\n" + Result.WarningMessage;
            return summary;
        }
    }

    public DelegateCommand StartCommand { get; }
    public DelegateCommand CancelCommand { get; }
    public DelegateCommand CloseCommand { get; }

    private async Task StartAsync()
    {
        Phase = DialogPhase.Progress;
        _cts = new CancellationTokenSource();

        var progress = new Progress<RestoreProgress>(p =>
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
            Result = await _orchestrator.RestoreAsync(Source, Target, DeleteTargetAfter, progress, _cts.Token);
        }
        catch (OperationCanceledException)
        {
            Result = new RestoreResult(false, "Abgebrochen.", null, 0, 0, TimeSpan.Zero);
        }
        catch (Exception ex)
        {
            Result = new RestoreResult(false, ex.Message, null, 0, 0, TimeSpan.Zero);
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

    private static string StageToLabel(RestoreStage stage) => stage switch
    {
        RestoreStage.PreFlight => "Prüfe Pfade…",
        RestoreStage.Staging => "Daten werden zurückkopiert…",
        RestoreStage.Verifying => "Verifiziere Kopie…",
        RestoreStage.RemovingJunction => "Entferne Junction…",
        RestoreStage.MovingStaging => "Verschiebe an Originalort…",
        RestoreStage.DeletingTarget => "Lösche Original-Ziel…",
        RestoreStage.Done => "Fertig.",
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
