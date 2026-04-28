// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using LinkMover.Core.Abstractions;
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
    private readonly IJunctionService _junctionService;
    private readonly IEventAggregator _eventAggregator;
    private CancellationTokenSource? _cts;
    private bool _isBusy;

    public InventoryListViewModel(
        IInventoryScanner scanner,
        IJunctionService junctionService,
        IEventAggregator eventAggregator)
    {
        _scanner = scanner;
        _junctionService = junctionService;
        _eventAggregator = eventAggregator;

        Entries = new ObservableCollection<InventoryEntry>();
        RefreshCommand = new DelegateCommand(async () => await RefreshAsync(), () => !IsBusy);
        OpenInExplorerCommand = new DelegateCommand<InventoryEntry>(OpenInExplorer);
        RemoveJunctionCommand = new DelegateCommand<InventoryEntry>(async entry => await RemoveJunctionAsync(entry));

        eventAggregator.GetEvent<JunctionCreatedEvent>().Subscribe(info =>
        {
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
    public DelegateCommand<InventoryEntry> OpenInExplorerCommand { get; }
    public DelegateCommand<InventoryEntry> RemoveJunctionCommand { get; }

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

    private static void OpenInExplorer(InventoryEntry entry)
    {
        if (entry is null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{entry.SourcePath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            // best effort — Explorer not opening shouldn't crash the app
        }
    }

    private async Task RemoveJunctionAsync(InventoryEntry entry)
    {
        if (entry is null) return;

        var confirm = MessageBox.Show(
            $"Junction wirklich entfernen?\n\n{entry.SourcePath}\n\nDas Ziel ({entry.TargetPath}) bleibt unverändert. " +
            $"Die Junction selbst wird gelöscht — Programme, die den ursprünglichen Pfad nutzen, finden danach nichts mehr.",
            "Junction entfernen",
            MessageBoxButton.OKCancel,
            MessageBoxImage.Warning);

        if (confirm != MessageBoxResult.OK) return;

        try
        {
            var removed = await _junctionService.RemoveJunctionAsync(entry.SourcePath);
            if (removed)
            {
                Entries.Remove(entry);
                _eventAggregator.GetEvent<JunctionRemovedEvent>().Publish(entry.SourcePath);
            }
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Junction konnte nicht entfernt werden:\n{ex.Message}", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
    }
}
