// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Diagnostics;
using System.Windows;
using System.Windows.Data;
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
    private const int SizeComputeParallelism = 4;

    private readonly IInventoryScanner _scanner;
    private readonly IJunctionService _junctionService;
    private readonly IJunctionRestoreOrchestrator _restoreOrchestrator;
    private readonly IJunctionRepointOrchestrator _repointOrchestrator;
    private readonly IFileSystemService _fileSystem;
    private readonly IEventAggregator _eventAggregator;
    private CancellationTokenSource? _cts;
    private bool _isBusy;
    private string _filterText = string.Empty;
    private string _scanCurrentPath = string.Empty;
    private int _scanFoundCount;
    private int _sizeProgress;
    private int _sizeTotal;
    private bool _sizesComputing;

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
        EntriesView = CollectionViewSource.GetDefaultView(Entries);
        EntriesView.Filter = MatchesFilter;

        // Refresh stays clickable even during a running scan so the user is
        // never staring at a dead button — clicking it cancels the in-flight
        // work and starts over.
        RefreshCommand = new DelegateCommand(async () => await RefreshAsync());
        CancelCommand = new DelegateCommand(Cancel, () => IsBusy || SizesComputing);
        OpenInExplorerCommand = new DelegateCommand<InventoryEntry>(OpenInExplorer);
        RemoveJunctionCommand = new DelegateCommand<InventoryEntry>(async entry => await RemoveJunctionAsync(entry));
        RestoreCommand = new DelegateCommand<InventoryEntry>(Restore);
        RepointCommand = new DelegateCommand<InventoryEntry>(Repoint);
        ClearFilterCommand = new DelegateCommand(() => FilterText = string.Empty);

        eventAggregator.GetEvent<JunctionCreatedEvent>().Subscribe(info =>
        {
            var entry = new InventoryEntry(info.Source, info.Target, info.Created, info.TargetExists);
            Entries.Add(entry);
            _ = ComputeSizeForEntryAsync(entry, _cts?.Token ?? CancellationToken.None);
        }, ThreadOption.UIThread, keepSubscriberReferenceAlive: true);
    }

    public ObservableCollection<InventoryEntry> Entries { get; }

    public ICollectionView EntriesView { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                CancelCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(StatusText));
                RaisePropertyChanged(nameof(HasStatus));
            }
        }
    }

    public string ScanCurrentPath
    {
        get => _scanCurrentPath;
        private set
        {
            if (SetProperty(ref _scanCurrentPath, value))
            {
                RaisePropertyChanged(nameof(StatusText));
            }
        }
    }

    public int ScanFoundCount
    {
        get => _scanFoundCount;
        private set
        {
            if (SetProperty(ref _scanFoundCount, value))
            {
                RaisePropertyChanged(nameof(StatusText));
            }
        }
    }

    public string FilterText
    {
        get => _filterText;
        set
        {
            if (SetProperty(ref _filterText, value))
            {
                EntriesView.Refresh();
                RaisePropertyChanged(nameof(VisibleCount));
            }
        }
    }

    public int VisibleCount => EntriesView.Cast<object>().Count();

    public int SizeProgress
    {
        get => _sizeProgress;
        private set
        {
            if (SetProperty(ref _sizeProgress, value))
            {
                RaisePropertyChanged(nameof(StatusText));
                RaisePropertyChanged(nameof(HasStatus));
            }
        }
    }

    public int SizeTotal
    {
        get => _sizeTotal;
        private set => SetProperty(ref _sizeTotal, value);
    }

    public bool SizesComputing
    {
        get => _sizesComputing;
        private set
        {
            if (SetProperty(ref _sizesComputing, value))
            {
                CancelCommand.RaiseCanExecuteChanged();
                RaisePropertyChanged(nameof(StatusText));
                RaisePropertyChanged(nameof(HasStatus));
            }
        }
    }

    public string StatusText
    {
        get
        {
            if (IsBusy)
            {
                var pathHint = string.IsNullOrEmpty(ScanCurrentPath) ? "…" : $" — {ScanCurrentPath}";
                return $"Suche Junctions ({ScanFoundCount} gefunden){pathHint}";
            }
            if (SizesComputing) return $"Berechne Größen… {SizeProgress} von {SizeTotal}";
            return string.Empty;
        }
    }

    public bool HasStatus => IsBusy || SizesComputing;

    public DelegateCommand RefreshCommand { get; }
    public DelegateCommand CancelCommand { get; }
    public DelegateCommand<InventoryEntry> OpenInExplorerCommand { get; }
    public DelegateCommand<InventoryEntry> RemoveJunctionCommand { get; }
    public DelegateCommand<InventoryEntry> RestoreCommand { get; }
    public DelegateCommand<InventoryEntry> RepointCommand { get; }
    public DelegateCommand ClearFilterCommand { get; }

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

    private bool MatchesFilter(object obj)
    {
        if (string.IsNullOrEmpty(_filterText)) return true;
        if (obj is not InventoryEntry e) return false;
        return e.SourcePath.Contains(_filterText, StringComparison.OrdinalIgnoreCase)
            || e.TargetPath.Contains(_filterText, StringComparison.OrdinalIgnoreCase);
    }

    private async Task RefreshAsync()
    {
        _cts?.Cancel();
        _cts = new CancellationTokenSource();
        var token = _cts.Token;

        Entries.Clear();
        ScanFoundCount = 0;
        ScanCurrentPath = string.Empty;
        IsBusy = true;

        var entryProgress = new Progress<InventoryEntry>(entry =>
        {
            Entries.Add(entry);
            ScanFoundCount = Entries.Count;
            RaisePropertyChanged(nameof(VisibleCount));
        });
        var pathProgress = new Progress<string>(p => ScanCurrentPath = p);

        try
        {
            await _scanner.ScanAsync(InventoryScannerDefaults.StandardRoots, entryProgress, pathProgress, token);

            // Size pass runs in the background so the grid is interactive immediately.
            _ = ComputeAllSizesAsync(token);
        }
        catch (OperationCanceledException)
        {
            // restarted or cancelled
        }
        catch (Exception ex)
        {
            MessageBox.Show($"Scan fehlgeschlagen: {ex.Message}", "Fehler",
                MessageBoxButton.OK, MessageBoxImage.Error);
        }
        finally
        {
            IsBusy = false;
            ScanCurrentPath = string.Empty;
        }
    }

    private void Cancel()
    {
        _cts?.Cancel();
    }

    private async Task ComputeAllSizesAsync(CancellationToken ct)
    {
        var entries = Entries.ToList();
        SizeTotal = entries.Count;
        SizeProgress = 0;
        SizesComputing = true;

        try
        {
            await Parallel.ForEachAsync(
                entries,
                new ParallelOptions { MaxDegreeOfParallelism = SizeComputeParallelism, CancellationToken = ct },
                async (entry, token) =>
                {
                    await ComputeSizeForEntryAsync(entry, token);
                    var done = Interlocked.Increment(ref _sizeProgress);

                    // Throttle UI ticks: every 5 entries (and the final one) is plenty
                    // of feedback without flooding the dispatcher with hundreds of updates.
                    if (done % 5 == 0 || done == entries.Count)
                    {
                        Application.Current.Dispatcher.BeginInvoke(() =>
                        {
                            RaisePropertyChanged(nameof(SizeProgress));
                            RaisePropertyChanged(nameof(StatusText));
                        });
                    }
                });
        }
        catch (OperationCanceledException)
        {
            // refresh / navigation cancelled — done
        }
        finally
        {
            SizesComputing = false;
        }
    }

    private async Task ComputeSizeForEntryAsync(InventoryEntry entry, CancellationToken ct)
    {
        long? sizeValue;
        if (!entry.TargetExists)
        {
            sizeValue = 0;
        }
        else
        {
            try
            {
                sizeValue = await _fileSystem.GetDirectorySizeAsync(entry.TargetPath, progress: null, ct);
            }
            catch (OperationCanceledException)
            {
                return;
            }
            catch
            {
                return;
            }
        }

        // Fire-and-forget UI dispatch so workers don't block on each property update.
        Application.Current.Dispatcher.BeginInvoke(() =>
        {
            entry.SizeBytes = sizeValue;
        });
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
                RaisePropertyChanged(nameof(VisibleCount));
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
            RaisePropertyChanged(nameof(VisibleCount));
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
