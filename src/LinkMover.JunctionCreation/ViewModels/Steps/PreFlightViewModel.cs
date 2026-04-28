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

public class PreFlightViewModel : BindableBase, INavigationAware
{
    private readonly IJunctionWizardOrchestrator _orchestrator;
    private readonly IRegionManager _regionManager;
    private CancellationTokenSource? _cts;
    private bool _isBusy;

    public PreFlightViewModel(IJunctionWizardOrchestrator orchestrator, IRegionManager regionManager)
    {
        _orchestrator = orchestrator;
        _regionManager = regionManager;

        ContinueCommand = new DelegateCommand(Continue, CanContinue);
        BackCommand = new DelegateCommand(Back);

        Context.PropertyChanged += (_, _) => ContinueCommand.RaiseCanExecuteChanged();
    }

    public WizardContext Context => _orchestrator.Context;

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ContinueCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DelegateCommand ContinueCommand { get; }
    public DelegateCommand BackCommand { get; }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public async void OnNavigatedTo(NavigationContext navigationContext)
    {
        IsBusy = true;
        _cts = new CancellationTokenSource();
        try
        {
            await _orchestrator.RunPreFlightAsync(_cts.Token);
        }
        catch (OperationCanceledException)
        {
            // user navigated away
        }
        catch (Exception ex)
        {
            Context.PreFlight = new PreFlightReport(new[]
            {
                new PreFlightCheck("Fehler", Severity.Error, $"Pre-Flight ist fehlgeschlagen: {ex.Message}")
            });
        }
        finally
        {
            IsBusy = false;
        }
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        _cts?.Cancel();
        _cts?.Dispose();
        _cts = null;
    }

    private bool CanContinue()
        => !IsBusy
           && Context.PreFlight is not null
           && !Context.PreFlight.IsBlocking;

    private void Continue()
        => _regionManager.RequestNavigate(RegionNames.WizardStep, ViewNames.WizardStep_Copy);

    private void Back()
        => _regionManager.RequestNavigate(RegionNames.WizardStep, ViewNames.WizardStep_SelectSource);
}
