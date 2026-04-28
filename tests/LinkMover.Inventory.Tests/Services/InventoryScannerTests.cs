// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.IO;
using LinkMover.Core.Services;
using LinkMover.Inventory.Models;
using LinkMover.Inventory.Services;

namespace LinkMover.Inventory.Tests.Services;

public class InventoryScannerTests
{
    private readonly InventoryScanner _sut;

    public InventoryScannerTests()
    {
        var fs = new FileSystemService();
        _sut = new InventoryScanner(fs, new JunctionService(fs));
    }

    private async Task<IReadOnlyList<InventoryEntry>> ScanAsync(IEnumerable<string> roots, CancellationToken ct)
    {
        var collector = new ListProgress<InventoryEntry>();
        await _sut.ScanAsync(roots, collector, onCurrentPath: null, ct);
        return collector.Items;
    }

    [Fact]
    public async Task ScanAsync_returns_empty_for_root_without_junctions()
    {
        using var fix = new TempDirectoryFixture();
        fix.CreateSubDir("plain");

        var result = await ScanAsync(new[] { fix.Root }, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ScanAsync_finds_junction_directly_below_root()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        var result = await ScanAsync(new[] { fix.Root }, CancellationToken.None);

        Assert.Single(result);
        var entry = result[0];
        Assert.Equal(Path.GetFullPath(link), entry.SourcePath);
        Assert.Equal(Path.GetFullPath(target), Path.GetFullPath(entry.TargetPath));
        Assert.True(entry.TargetExists);
    }

    [Fact]
    public async Task ScanAsync_finds_junction_in_nested_subdirectory()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var nested = fix.CreateSubDir("a/b/c");
        var link = Path.Combine(nested, "link");
        fix.CreateJunction(link, target);

        var result = await ScanAsync(new[] { fix.Root }, CancellationToken.None);

        Assert.Single(result);
        Assert.Equal(Path.GetFullPath(link), result[0].SourcePath);
    }

    [Fact]
    public async Task ScanAsync_deduplicates_when_same_root_passed_twice()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        var result = await ScanAsync(new[] { fix.Root, fix.Root }, CancellationToken.None);

        Assert.Single(result);
    }

    [Fact]
    public async Task ScanAsync_marks_dangling_junction_with_target_missing()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);
        Directory.Delete(target);

        var result = await ScanAsync(new[] { fix.Root }, CancellationToken.None);

        Assert.Single(result);
        Assert.False(result[0].TargetExists);
    }

    [Fact]
    public async Task ScanAsync_skips_roots_that_do_not_exist()
    {
        using var fix = new TempDirectoryFixture();
        var nonExistent = Path.Combine(Path.GetTempPath(), "lm-missing-" + Guid.NewGuid().ToString("N"));

        var result = await ScanAsync(new[] { nonExistent, fix.Root }, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task ScanAsync_reports_current_path_progress()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        var paths = new ListProgress<string>();
        await _sut.ScanAsync(new[] { fix.Root }, onEntry: null, onCurrentPath: paths, CancellationToken.None);

        Assert.Contains(fix.Root, paths.Items);
    }

    private sealed class ListProgress<T> : IProgress<T>
    {
        private readonly List<T> _items = new();
        public IReadOnlyList<T> Items
        {
            get { lock (_items) { return _items.ToList(); } }
        }

        public void Report(T value)
        {
            lock (_items) { _items.Add(value); }
        }
    }
}
