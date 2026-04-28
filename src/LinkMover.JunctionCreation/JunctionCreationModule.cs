// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Navigation;
using LinkMover.JunctionCreation.ViewModels;
using LinkMover.JunctionCreation.ViewModels.Steps;
using LinkMover.JunctionCreation.Views;
using LinkMover.JunctionCreation.Views.Steps;
using LinkMover.JunctionCreation.Wizard;
using Prism.Ioc;
using Prism.Modularity;

namespace LinkMover.JunctionCreation;

public class JunctionCreationModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider) { }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IJunctionWizardOrchestrator, JunctionWizardOrchestrator>();

        containerRegistry.RegisterForNavigation<JunctionWizardHostView, JunctionWizardHostViewModel>(ViewNames.JunctionWizardHost);
        containerRegistry.RegisterForNavigation<SelectSourceView, SelectSourceViewModel>(ViewNames.WizardStep_SelectSource);
        containerRegistry.RegisterForNavigation<PreFlightView, PreFlightViewModel>(ViewNames.WizardStep_PreFlight);
    }
}
