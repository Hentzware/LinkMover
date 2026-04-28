// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Events;
using LinkMover.Core.Navigation;
using LinkMover.JunctionCreation.Wizard;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace LinkMover.JunctionCreation.ViewModels.Steps;

public class DoneViewModel : BindableBase, INavigationAware
{
    private readonly IJunctionWizardOrchestrator _orchestrator;
    private readonly IRegionManager _regionManager;
    private readonly IEventAggregator _eventAggregator;

    public DoneViewModel(
        IJunctionWizardOrchestrator orchestrator,
        IRegionManager regionManager,
        IEventAggregator eventAggregator)
    {
        _orchestrator = orchestrator;
        _regionManager = regionManager;
        _eventAggregator = eventAggregator;

        RestartCommand = new DelegateCommand(Restart);
        OpenInventoryCommand = new DelegateCommand(OpenInventory);
    }

    public WizardContext Context => _orchestrator.Context;

    public string Headline
        => Context.DryRun
            ? "Trockenlauf abgeschlossen — keine Änderungen geschrieben."
            : "Junction erfolgreich erstellt.";

    public string BytesText => Context.CopyResult is null ? "—" : Format(Context.CopyResult.BytesCopied);
    public string FilesText => Context.CopyResult is null ? "—" : Context.CopyResult.FilesCopied.ToString();
    public string ElapsedText => Context.CopyResult is null ? "—" : Context.CopyResult.Elapsed.ToString(@"hh\:mm\:ss");

    public DelegateCommand RestartCommand { get; }
    public DelegateCommand OpenInventoryCommand { get; }

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public void OnNavigatedTo(NavigationContext navigationContext)
    {
        if (!Context.DryRun && Context.JunctionInfo is not null)
        {
            _eventAggregator.GetEvent<JunctionCreatedEvent>().Publish(Context.JunctionInfo);
        }
    }

    public void OnNavigatedFrom(NavigationContext navigationContext) { }

    private void Restart()
    {
        _orchestrator.Reset();
        _regionManager.RequestNavigate(RegionNames.WizardStep, ViewNames.WizardStep_SelectSource);
    }

    private void OpenInventory()
    {
        _regionManager.RequestNavigate(RegionNames.Content, ViewNames.InventoryList);
    }

    private static string Format(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
