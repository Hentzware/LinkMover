// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Navigation;
using LinkMover.JunctionCreation.Wizard;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace LinkMover.JunctionCreation.ViewModels.Steps;

public class CleanupViewModel : BindableBase, INavigationAware
{
    private readonly IJunctionWizardOrchestrator _orchestrator;
    private readonly IRegionManager _regionManager;
    private CancellationTokenSource? _cts;
    private bool _isBusy;
    private string _statusText = string.Empty;

    public CleanupViewModel(IJunctionWizardOrchestrator orchestrator, IRegionManager regionManager)
    {
        _orchestrator = orchestrator;
        _regionManager = regionManager;

        DeleteCommand = new DelegateCommand(async () => await DeleteAsync());
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
                RaisePropertyChanged(nameof(CanInteract));
            }
        }
    }

    public bool CanInteract => !IsBusy;

    public string StatusText
    {
        get => _statusText;
        set
        {
            if (SetProperty(ref _statusText, value))
            {
                RaisePropertyChanged(nameof(HasStatus));
            }
        }
    }

    public bool HasStatus => !string.IsNullOrEmpty(StatusText);

    public DelegateCommand DeleteCommand { get; }
    public DelegateCommand AbortCommand { get; }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;
    public void OnNavigatedTo(NavigationContext navigationContext) { }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task DeleteAsync()
    {
        IsBusy = true;
        StatusText = "Quelle wird gelöscht…";
        _cts = new CancellationTokenSource();
        try
        {
            await _orchestrator.RunCleanupAsync(_cts.Token);
            IsBusy = false;
            _regionManager.RequestNavigate(RegionNames.WizardStep, ViewNames.WizardStep_CreateLink);
        }
        catch (Exception ex)
        {
            IsBusy = false;
            StatusText = $"Löschen fehlgeschlagen: {ex.Message}";
        }
    }

    private void Abort()
    {
        _orchestrator.Reset();
        _regionManager.RequestNavigate(RegionNames.WizardStep, ViewNames.WizardStep_SelectSource);
    }
}
