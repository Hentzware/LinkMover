// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Collections.ObjectModel;
using LinkMover.Core.Events;
using LinkMover.Inventory.Models;
using LinkMover.Inventory.Services;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace LinkMover.Inventory.ViewModels;

public class InventoryListViewModel : BindableBase, INavigationAware
{
    private readonly IInventoryScanner _scanner;
    private CancellationTokenSource? _cts;
    private bool _isBusy;

    public InventoryListViewModel(IInventoryScanner scanner, IEventAggregator eventAggregator)
    {
        _scanner = scanner;

        Entries = new ObservableCollection<InventoryEntry>();
        RefreshCommand = new DelegateCommand(async () => await RefreshAsync(), () => !IsBusy);

        eventAggregator.GetEvent<JunctionCreatedEvent>().Subscribe(info =>
        {
            // New junction landed — append directly so the user sees it without a full re-scan.
            Entries.Add(new InventoryEntry(info.Source, info.Target, info.Created, info.TargetExists));
        }, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);
    }

    public ObservableCollection<InventoryEntry> Entries { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                RefreshCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public DelegateCommand RefreshCommand { get; }

    public string Title => "Inventar bestehender Junctions";

    public bool IsNavigationTarget(NavigationContext navigationContext) => true;

    public async void OnNavigatedTo(NavigationContext navigationContext)
    {
        if (Entries.Count == 0)
        {
            await RefreshAsync();
        }
    }

    public void OnNavigatedFrom(NavigationContext navigationContext)
    {
        _cts?.Cancel();
    }

    private async Task RefreshAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        IsBusy = true;
        try
        {
            var result = await _scanner.ScanAsync(InventoryScannerDefaults.StandardRoots, _cts.Token);
            Entries.Clear();
            foreach (var entry in result)
            {
                Entries.Add(entry);
            }
        }
        catch (OperationCanceledException)
        {
            // navigated away
        }
        finally
        {
            IsBusy = false;
        }
    }
}
