// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.IO;
using LinkMover.Core.Navigation;
using LinkMover.JunctionCreation.Wizard;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace LinkMover.JunctionCreation.ViewModels.Steps;

public class SelectSourceViewModel : BindableBase
{
    private readonly IJunctionWizardOrchestrator _orchestrator;
    private readonly IRegionManager _regionManager;

    public SelectSourceViewModel(IJunctionWizardOrchestrator orchestrator, IRegionManager regionManager)
    {
        _orchestrator = orchestrator;
        _regionManager = regionManager;

        BrowseSourceCommand = new DelegateCommand(BrowseSource);
        BrowseTargetCommand = new DelegateCommand(BrowseTarget);
        ContinueCommand = new DelegateCommand(Continue, CanContinue);

        Context.PropertyChanged += (_, _) => ContinueCommand.RaiseCanExecuteChanged();
    }

    public WizardContext Context => _orchestrator.Context;

    public DelegateCommand BrowseSourceCommand { get; }
    public DelegateCommand BrowseTargetCommand { get; }
    public DelegateCommand ContinueCommand { get; }

    private bool CanContinue()
        => !string.IsNullOrWhiteSpace(Context.Source)
           && !string.IsNullOrWhiteSpace(Context.Target);

    private void Continue()
        => _regionManager.RequestNavigate(RegionNames.WizardStep, ViewNames.WizardStep_PreFlight);

    private void BrowseSource()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Quell-Verzeichnis wählen",
            InitialDirectory = SafeInitialDirectory(Context.Source)
        };
        if (dialog.ShowDialog() == true)
        {
            Context.Source = dialog.FolderName;
        }
    }

    private void BrowseTarget()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Ziel-Verzeichnis wählen (existiert noch nicht oder ist leer)",
            InitialDirectory = SafeInitialDirectory(Context.Target)
        };
        if (dialog.ShowDialog() == true)
        {
            Context.Target = dialog.FolderName;
        }
    }

    private static string SafeInitialDirectory(string path)
    {
        if (!string.IsNullOrEmpty(path) && Directory.Exists(path))
        {
            return path;
        }
        return Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    }
}
