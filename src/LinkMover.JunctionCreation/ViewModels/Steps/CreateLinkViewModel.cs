// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Navigation;
using LinkMover.JunctionCreation.Wizard;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace LinkMover.JunctionCreation.ViewModels.Steps;

public class CreateLinkViewModel : BindableBase, INavigationAware
{
    private readonly IJunctionWizardOrchestrator _orchestrator;
    private readonly IRegionManager _regionManager;
    private CancellationTokenSource? _cts;
    private string _statusText = string.Empty;
    private bool _showError;

    public CreateLinkViewModel(IJunctionWizardOrchestrator orchestrator, IRegionManager regionManager)
    {
        _orchestrator = orchestrator;
        _regionManager = regionManager;
        RetryCommand = new DelegateCommand(async () => await RunAsync());
    }

    public WizardContext Context => _orchestrator.Context;

    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public bool ShowError { get => _showError; set => SetProperty(ref _showError, value); }

    public DelegateCommand RetryCommand { get; }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public async void OnNavigatedTo(NavigationContext navigationContext)
    {
        await RunAsync();
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private async Task RunAsync()
    {
        ShowError = false;
        StatusText = "Junction wird erstellt…";
        _cts = new CancellationTokenSource();
        try
        {
            await _orchestrator.RunCreateLinkAsync(_cts.Token);
            _regionManager.RequestNavigate(RegionNames.WizardStep, ViewNames.WizardStep_Done);
        }
        catch (OperationCanceledException)
        {
            // user navigated away
        }
        catch (Exception ex)
        {
            StatusText = $"Junction-Erstellung fehlgeschlagen: {ex.Message}";
            ShowError = true;
        }
    }
}
