// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Events;
using LinkMover.Core.Navigation;
using LinkMover.JunctionCreation.ViewModels;
using LinkMover.JunctionCreation.ViewModels.Steps;
using LinkMover.JunctionCreation.Views;
using LinkMover.JunctionCreation.Views.Steps;
using LinkMover.JunctionCreation.Wizard;
using Prism.Events;
using Prism.Ioc;
using Prism.Modularity;
using Prism.Navigation.Regions;

namespace LinkMover.JunctionCreation;

public class JunctionCreationModule : IModule
{
    public void OnInitialized(IContainerProvider containerProvider)
    {
        var eventAggregator = containerProvider.Resolve<IEventAggregator>();
        var orchestrator = containerProvider.Resolve<IJunctionWizardOrchestrator>();
        var regionManager = containerProvider.Resolve<IRegionManager>();

        eventAggregator.GetEvent<CreateJunctionRequestedEvent>().Subscribe(plan =>
        {
            orchestrator.Reset();
            orchestrator.Context.Source = plan.Source;
            orchestrator.Context.Target = plan.Target;
            orchestrator.Context.DryRun = plan.DryRun;
            regionManager.RequestNavigate(RegionNames.Content, ViewNames.JunctionWizardHost);
        }, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);
    }

    public void RegisterTypes(IContainerRegistry containerRegistry)
    {
        containerRegistry.RegisterSingleton<IJunctionWizardOrchestrator, JunctionWizardOrchestrator>();

        containerRegistry.RegisterForNavigation<JunctionWizardHostView, JunctionWizardHostViewModel>(ViewNames.JunctionWizardHost);
        containerRegistry.RegisterForNavigation<SelectSourceView, SelectSourceViewModel>(ViewNames.WizardStep_SelectSource);
        containerRegistry.RegisterForNavigation<PreFlightView, PreFlightViewModel>(ViewNames.WizardStep_PreFlight);
        containerRegistry.RegisterForNavigation<CopyView, CopyViewModel>(ViewNames.WizardStep_Copy);
        containerRegistry.RegisterForNavigation<VerifyView, VerifyViewModel>(ViewNames.WizardStep_Verify);
        containerRegistry.RegisterForNavigation<CleanupView, CleanupViewModel>(ViewNames.WizardStep_Cleanup);
        containerRegistry.RegisterForNavigation<CreateLinkView, CreateLinkViewModel>(ViewNames.WizardStep_CreateLink);
        containerRegistry.RegisterForNavigation<DoneView, DoneViewModel>(ViewNames.WizardStep_Done);
    }
}
