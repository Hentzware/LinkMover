// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Navigation;
using LinkMover.JunctionCreation.Wizard;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace LinkMover.JunctionCreation.ViewModels.Steps;

public class VerifyViewModel : BindableBase, INavigationAware
{
    private readonly IJunctionWizardOrchestrator _orchestrator;
    private readonly IRegionManager _regionManager;
    private CancellationTokenSource? _cts;
    private bool _isBusy;

    public VerifyViewModel(IJunctionWizardOrchestrator orchestrator, IRegionManager regionManager)
    {
        _orchestrator = orchestrator;
        _regionManager = regionManager;

        ContinueCommand = new DelegateCommand(Continue);
        AbortCommand = new DelegateCommand(Abort);
    }

    public WizardContext Context => _orchestrator.Context;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RaisePropertyChanged(nameof(HasResult));
                RaisePropertyChanged(nameof(ShowContinue));
                RaisePropertyChanged(nameof(ShowAbort));
            }
        }
    }

    public bool HasResult => !IsBusy && Context.VerifyResult is not null;

    public bool ShowContinue => HasResult && Context.VerifyResult!.Match;

    public bool ShowAbort => HasResult && !Context.VerifyResult!.Match;

    public bool HasMissingFiles
        => Context.VerifyResult is { } v && v.MissingFiles.Count > 0;

    public string ResultHeadline
        => Context.VerifyResult is null
            ? string.Empty
            : Context.VerifyResult.Match
                ? "Verifikation erfolgreich — Quelle und Ziel stimmen überein."
                : "Verifikation fehlgeschlagen — Quelle bleibt intakt, kein Aufräumen.";

    public string SourceBytesFormatted
        => Context.VerifyResult is null ? string.Empty : Format(Context.VerifyResult.SourceBytes);

    public string TargetBytesFormatted
        => Context.VerifyResult is null ? string.Empty : Format(Context.VerifyResult.TargetBytes);

    public DelegateCommand ContinueCommand { get; }
    public DelegateCommand AbortCommand { get; }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public async void OnNavigatedTo(NavigationContext navigationContext)
    {
        IsBusy = true;
        _cts = new CancellationTokenSource();
        try
        {
            await _orchestrator.RunVerifyAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // navigated away
        }
        finally
        {
            IsBusy = false;
            RaisePropertyChanged(nameof(HasResult));
            RaisePropertyChanged(nameof(ShowContinue));
            RaisePropertyChanged(nameof(ShowAbort));
            RaisePropertyChanged(nameof(HasMissingFiles));
            RaisePropertyChanged(nameof(ResultHeadline));
            RaisePropertyChanged(nameof(SourceBytesFormatted));
            RaisePropertyChanged(nameof(TargetBytesFormatted));
        }
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private void Continue()
        => _regionManager.RequestNavigate(RegionNames.WizardStep, ViewNames.WizardStep_Cleanup);

    private void Abort()
    {
        _orchestrator.Reset();
        _regionManager.RequestNavigate(RegionNames.WizardStep, ViewNames.WizardStep_SelectSource);
    }

    private static string Format(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
