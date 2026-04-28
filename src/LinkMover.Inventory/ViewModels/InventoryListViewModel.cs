// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.Windows;
using LinkMover.Core.Abstractions;
using LinkMover.Core.Events;
using LinkMover.Inventory.Models;
using LinkMover.Inventory.Repoint;
using LinkMover.Inventory.Restore;
using LinkMover.Inventory.Services;
using LinkMover.Inventory.Views;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;
using Prism.Navigation.Regions;

namespace LinkMover.Inventory.ViewModels;

public class InventoryListViewModel : BindableBase, INavigationAware
{
    private readonly IInventoryScanner _scanner;
    private readonly IJunctionService _junctionService;
    private readonly IJunctionRestoreOrchestrator _restoreOrchestrator;
    private readonly IJunctionRepointOrchestrator _repointOrchestrator;
    private readonly IFileSystemService _fileSystem;
    private readonly IEventAggregator _eventAggregator;
    private CancellationTokenSource? _cts;
    private bool _isBusy;

    public InventoryListViewModel(
        IInventoryScanner scanner,
        IJunctionService junctionService,
        IJunctionRestoreOrchestrator restoreOrchestrator,
        IJunctionRepointOrchestrator repointOrchestrator,
        IFileSystemService fileSystem,
        IEventAggregator eventAggregator)
    {
        _scanner = scanner;
        _junctionService = junctionService;
        _restoreOrchestrator = restoreOrchestrator;
        _repointOrchestrator = repointOrchestrator;
        _fileSystem = fileSystem;
        _eventAggregator = eventAggregator;

        Entries = new ObservableCollection<InventoryEntry>();
        RefreshCommand = new DelegateCommand(async () => await RefreshAsync(), () => !IsBusy);
        OpenInExplorerCommand = new DelegateCommand<InventoryEntry>(OpenInExplorer);
        RemoveJunctionCommand = new DelegateCommand<InventoryEntry>(async entry => await RemoveJunctionAsync(entry));
        RestoreCommand = new DelegateCommand<InventoryEntry>(Restore);
        RepointCommand = new DelegateCommand<InventoryEntry>(Repoint);

        eventAggregator.GetEvent<JunctionCreatedEvent>().Subscribe(info =>
        {
            var entry = new InventoryEntry(info.Source, info.Target, info.Created, info.TargetExists);
            Entries.Add(entry);
            _ = ComputeSizeForEntryAsync(entry, _cts?.Token ?? CancellationToken.None);
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
    public DelegateCommand<InventoryEntry> RestoreCommand { get; }
    public DelegateCommand<InventoryEntry> RepointCommand { get; }

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

            // Sizes are computed lazily so the grid is responsive immediately.
            // Each entry's SizeBytes setter raises PropertyChanged on SizeText,
            // so the row updates the moment its size lands.
            _ = ComputeAllSizesAsync(_cts.Token);
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

    private async Task ComputeAllSizesAsync(CancellationToken ct)
    {
        foreach (var entry in Entries.ToList())
        {
            if (ct.IsCancellationRequested) break;
            await ComputeSizeForEntryAsync(entry, ct);
        }
    }

    private async Task ComputeSizeForEntryAsync(InventoryEntry entry, CancellationToken ct)
    {
        if (!entry.TargetExists)
        {
            entry.SizeBytes = 0;
            return;
        }
        try
        {
            var size = await _fileSystem.GetDirectorySizeAsync(entry.TargetPath, progress: null, ct);
            entry.SizeBytes = size;
        }
        catch (OperationCanceledException)
        {
            // refresh happened — leave size for the next pass
        }
        catch
        {
            // permission denied, target gone mid-walk, etc. — leave size as null ("…")
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

    private void Restore(InventoryEntry entry)
    {
        if (entry is null) return;

        var dialog = new RestoreDialog
        {
            DataContext = new RestoreDialogViewModel(_restoreOrchestrator, entry.SourcePath, entry.TargetPath),
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            Entries.Remove(entry);
            _eventAggregator.GetEvent<JunctionRemovedEvent>().Publish(entry.SourcePath);
        }
    }

    private void Repoint(InventoryEntry entry)
    {
        if (entry is null) return;

        var vm = new RepointDialogViewModel(_repointOrchestrator, entry.SourcePath, entry.TargetPath);
        var dialog = new RepointDialog
        {
            DataContext = vm,
            Owner = Application.Current.MainWindow
        };

        if (dialog.ShowDialog() == true)
        {
            // Junction now points at the new target — refresh the row in place.
            var info = _junctionService.Inspect(entry.SourcePath);
            if (info is not null)
            {
                var index = Entries.IndexOf(entry);
                if (index >= 0)
                {
                    var fresh = new InventoryEntry(info.Source, info.Target, info.Created, info.TargetExists);
                    Entries[index] = fresh;
                    _ = ComputeSizeForEntryAsync(fresh, _cts?.Token ?? CancellationToken.None);
                }
            }
        }
    }
}
