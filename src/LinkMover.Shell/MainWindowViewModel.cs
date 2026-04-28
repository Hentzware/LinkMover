// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Collections.ObjectModel;
using LinkMover.Core.Navigation;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace LinkMover.Shell;

public class MainWindowViewModel : BindableBase
{
    private readonly IRegionManager _regionManager;
    private NavigationItem? _selectedItem;

    public MainWindowViewModel(IRegionManager regionManager)
    {
        _regionManager = regionManager;
        NavigationItems = new ObservableCollection<NavigationItem>
        {
            new("Erstellen", ViewNames.JunctionWizardHost),
            new("Inventar", ViewNames.InventoryList),
            new("Analyzer", ViewNames.AnalyzerHome),
        };
        SelectedItem = NavigationItems[0];
    }

    public ObservableCollection<NavigationItem> NavigationItems { get; }

    public NavigationItem? SelectedItem
    {
        get => _selectedItem;
        set
        {
            if (SetProperty(ref _selectedItem, value) && value is not null)
            {
                _regionManager.RequestNavigate(RegionNames.Content, value.ViewName);
            }
        }
    }
}

public sealed record NavigationItem(string Title, string ViewName);
