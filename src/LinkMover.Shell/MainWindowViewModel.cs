// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Collections.ObjectModel;
using System.Collections.Specialized;
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

        regionManager.Regions.CollectionChanged += OnRegionsChanged;
        if (regionManager.Regions.ContainsRegionWithName(RegionNames.Content))
        {
            regionManager.Regions[RegionNames.Content].NavigationService.Navigated += OnContentNavigated;
        }

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

    private void OnRegionsChanged(object? sender, NotifyCollectionChangedEventArgs e)
    {
        if (e.Action != NotifyCollectionChangedAction.Add || e.NewItems is null)
        {
            return;
        }
        foreach (var item in e.NewItems)
        {
            if (item is IRegion region && region.Name == RegionNames.Content)
            {
                region.NavigationService.Navigated += OnContentNavigated;
            }
        }
    }

    private void OnContentNavigated(object? sender, RegionNavigationEventArgs e)
    {
        var viewName = e.Uri.OriginalString;
        var queryStart = viewName.IndexOf('?');
        if (queryStart >= 0)
        {
            viewName = viewName[..queryStart];
        }

        var match = NavigationItems.FirstOrDefault(i => i.ViewName == viewName);
        if (match is null || ReferenceEquals(_selectedItem, match))
        {
            return;
        }

        // Direct field write + RaisePropertyChanged: avoids re-entering the
        // SelectedItem setter, which would call RequestNavigate again.
        _selectedItem = match;
        RaisePropertyChanged(nameof(SelectedItem));
    }
}

public sealed record NavigationItem(string Title, string ViewName);
