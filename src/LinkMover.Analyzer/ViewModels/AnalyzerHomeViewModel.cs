// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.Collections.ObjectModel;
using System.Diagnostics;
using System.IO;
using LinkMover.Analyzer.Models;
using LinkMover.Core.Abstractions;
using LinkMover.Core.Events;
using LinkMover.Core.Models;
using Microsoft.Win32;
using Prism.Commands;
using Prism.Events;
using Prism.Mvvm;

namespace LinkMover.Analyzer.ViewModels;

public class AnalyzerHomeViewModel : BindableBase
{
    private readonly IDiskScanner _scanner;
    private readonly IEventAggregator _eventAggregator;
    private CancellationTokenSource? _cts;
    private string _path = Environment.GetFolderPath(Environment.SpecialFolder.UserProfile);
    private bool _isBusy;
    private string _statusText = string.Empty;
    private long _filesScanned;
    private long _bytesScanned;

    public AnalyzerHomeViewModel(IDiskScanner scanner, IEventAggregator eventAggregator)
    {
        _scanner = scanner;
        _eventAggregator = eventAggregator;
        Roots = new ObservableCollection<TreeNodeViewModel>();
        ScanCommand = new DelegateCommand(async () => await ScanAsync(), CanScan);
        CancelCommand = new DelegateCommand(Cancel, () => IsBusy);
        BrowseCommand = new DelegateCommand(Browse);
        RequestJunctionCommand = new DelegateCommand<TreeNodeViewModel>(RequestJunction);
        OpenInExplorerCommand = new DelegateCommand<TreeNodeViewModel>(OpenInExplorer);
    }

    public string Title => "Speicher-Analyzer";

    public string Path
    {
        get => _path;
        set
        {
            if (SetProperty(ref _path, value))
            {
                ScanCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public ObservableCollection<TreeNodeViewModel> Roots { get; }

    public bool IsBusy
    {
        get => _isBusy;
        set
        {
            if (SetProperty(ref _isBusy, value))
            {
                ScanCommand.RaiseCanExecuteChanged();
                CancelCommand.RaiseCanExecuteChanged();
            }
        }
    }

    public string StatusText { get => _statusText; set => SetProperty(ref _statusText, value); }
    public long FilesScanned { get => _filesScanned; set => SetProperty(ref _filesScanned, value); }
    public long BytesScanned { get => _bytesScanned; set => SetProperty(ref _bytesScanned, value); }

    public DelegateCommand ScanCommand { get; }
    public DelegateCommand CancelCommand { get; }
    public DelegateCommand BrowseCommand { get; }
    public DelegateCommand<TreeNodeViewModel> RequestJunctionCommand { get; }
    public DelegateCommand<TreeNodeViewModel> OpenInExplorerCommand { get; }

    private bool CanScan() => !IsBusy && !string.IsNullOrWhiteSpace(Path) && Directory.Exists(Path);

    private async Task ScanAsync()
    {
        IsBusy = true;
        StatusText = "Scan läuft…";
        FilesScanned = 0;
        BytesScanned = 0;
        Roots.Clear();
        _cts = new CancellationTokenSource();

        var progress = new Progress<ScanProgress>(p =>
        {
            FilesScanned = p.ScannedItems;
            BytesScanned = p.ScannedBytes;
            StatusText = $"Scanne {p.CurrentPath}";
        });

        try
        {
            var rootNode = await _scanner.ScanRecursiveAsync(Path, progress, _cts.Token);

            foreach (var child in rootNode.Children.OrderByDescending(c => c.SizeBytes))
            {
                Roots.Add(new TreeNodeViewModel(child, rootNode.SizeBytes));
            }

            FilesScanned = rootNode.FileCount;
            BytesScanned = rootNode.SizeBytes;
            StatusText = $"Fertig — {rootNode.FileCount:N0} Dateien, {FormatBytes(rootNode.SizeBytes)} insgesamt.";
        }
        catch (OperationCanceledException)
        {
            StatusText = "Scan abgebrochen.";
            Roots.Clear();
        }
        catch (Exception ex)
        {
            StatusText = $"Scan fehlgeschlagen: {ex.Message}";
        }
        finally
        {
            IsBusy = false;
        }
    }

    private void Cancel() => _cts?.Cancel();

    private void Browse()
    {
        var dialog = new OpenFolderDialog
        {
            Title = "Verzeichnis zum Scannen wählen",
            InitialDirectory = Directory.Exists(Path) ? Path : Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)
        };
        if (dialog.ShowDialog() == true)
        {
            Path = dialog.FolderName;
        }
    }

    private void RequestJunction(TreeNodeViewModel node)
    {
        if (node is null) return;
        var plan = new JunctionPlan(
            Source: node.FullPath,
            Target: SuggestTarget(node.FullPath));
        _eventAggregator.GetEvent<CreateJunctionRequestedEvent>().Publish(plan);
    }

    private static void OpenInExplorer(TreeNodeViewModel node)
    {
        if (node is null) return;
        try
        {
            Process.Start(new ProcessStartInfo
            {
                FileName = "explorer.exe",
                Arguments = $"\"{node.FullPath}\"",
                UseShellExecute = true
            });
        }
        catch
        {
            // best effort
        }
    }

    private static string SuggestTarget(string sourcePath)
    {
        var leaf = System.IO.Path.GetFileName(sourcePath);
        if (string.IsNullOrEmpty(leaf)) leaf = "data";

        // Pick the first ready, fixed, non-system drive — or fall back to D:\ as a hint.
        var preferredRoot = DriveInfo.GetDrives()
            .Where(d => d.IsReady && d.DriveType == DriveType.Fixed)
            .Where(d => !d.RootDirectory.FullName.StartsWith("C:", StringComparison.OrdinalIgnoreCase))
            .Select(d => d.RootDirectory.FullName)
            .FirstOrDefault() ?? "D:\\";

        return System.IO.Path.Combine(preferredRoot, "AppData", leaf);
    }

    private static string FormatBytes(long bytes)
    {
        if (bytes < 1024) return $"{bytes} B";
        if (bytes < 1024L * 1024) return $"{bytes / 1024.0:F1} KB";
        if (bytes < 1024L * 1024 * 1024) return $"{bytes / (1024.0 * 1024):F1} MB";
        return $"{bytes / (1024.0 * 1024 * 1024):F2} GB";
    }
}
