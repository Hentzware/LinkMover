// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Windows.Input;
using LinkMover.Core.Navigation;
using LinkMover.JunctionCreation.Wizard;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace LinkMover.JunctionCreation.ViewModels;

public class JunctionWizardHostViewModel : BindableBase
{
    private readonly IRegionManager _regionManager;
    private readonly IJunctionWizardOrchestrator _orchestrator;
    private string _stepLabel = string.Empty;

    public JunctionWizardHostViewModel(IRegionManager regionManager, IJunctionWizardOrchestrator orchestrator)
    {
        _regionManager = regionManager;
        _orchestrator = orchestrator;
        CancelCommand = new DelegateCommand(Cancel);
    }

    public string StepLabel
    {
        get => _stepLabel;
        set => SetProperty(ref _stepLabel, value);
    }

    public ICommand CancelCommand { get; }

    public void OnViewLoaded()
    {
        _orchestrator.Reset();
        StepLabel = "Schritt 1 von 7 — Quelle und Ziel wählen";
        _regionManager.RequestNavigate(RegionNames.WizardStep, ViewNames.WizardStep_SelectSource);
    }

    private void Cancel()
    {
        _orchestrator.Reset();
        StepLabel = "Schritt 1 von 7 — Quelle und Ziel wählen";
        _regionManager.RequestNavigate(RegionNames.WizardStep, ViewNames.WizardStep_SelectSource);
    }
}
