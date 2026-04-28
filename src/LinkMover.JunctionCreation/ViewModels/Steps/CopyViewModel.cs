// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Models;
using LinkMover.Core.Navigation;
using LinkMover.JunctionCreation.Wizard;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace LinkMover.JunctionCreation.ViewModels.Steps;

public class CopyViewModel : BindableBase, INavigationAware
{
    private readonly IJunctionWizardOrchestrator _orchestrator;
    private readonly IRegionManager _regionManager;
    private CancellationTokenSource? _cts;
    private bool _isBusy;
    private string _currentFile = string.Empty;
    private long _bytesCopied;
    private string _errorMessage = string.Empty;

    public CopyViewModel(IJunctionWizardOrchestrator orchestrator, IRegionManager regionManager)
    {
        _orchestrator = orchestrator;
        _regionManager = regionManager;
        CancelCommand = new DelegateCommand(Cancel);
    }

    public WizardContext Context => _orchestrator.Context;

    public bool IsBusy { get => _isBusy; set => SetProperty(ref _isBusy, value); }
    public string CurrentFile { get => _currentFile; set => SetProperty(ref _currentFile, value); }
    public long BytesCopied { get => _bytesCopied; set => SetProperty(ref _bytesCopied, value); }
    public string ErrorMessage { get => _errorMessage; set => SetProperty(ref _errorMessage, value); }

    public long TotalBytes => Context.SourceSizeBytes;
    public bool IsIndeterminate => TotalBytes <= 0;

    public string ProgressLabel
        => TotalBytes > 0
            ? $"{Format(BytesCopied)} von {Format(TotalBytes)}"
            : Format(BytesCopied);

    public DelegateCommand CancelCommand { get; }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public async void OnNavigatedTo(NavigationContext navigationContext)
    {
        IsBusy = true;
        ErrorMessage = string.Empty;
        BytesCopied = 0;
        CurrentFile = string.Empty;
        _cts = new CancellationTokenSource();

        RaisePropertyChanged(nameof(TotalBytes));
        RaisePropertyChanged(nameof(IsIndeterminate));
        RaisePropertyChanged(nameof(ProgressLabel));

        var progress = new Progress<RobocopyProgress>(p =>
        {
            CurrentFile = p.CurrentFile;
            BytesCopied = p.TotalBytesCopied;
            RaisePropertyChanged(nameof(ProgressLabel));
        });

        try
        {
            await _orchestrator.RunCopyAsync(progress, _cts.Token);
            IsBusy = false;
            _regionManager.RequestNavigate(RegionNames.WizardStep, ViewNames.WizardStep_Verify);
        }
        catch (OperationCanceledException)
        {
            IsBusy = false;
        }
        catch (Exception ex)
        {
            IsBusy = false;
            ErrorMessage = ex.Message;
        }
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private void Cancel()
    {
        _cts?.Cancel();
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
