// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Models;
using LinkMover.Core.Services;

namespace LinkMover.Core.Tests.Services;

public class DiskScannerTests
{
    private readonly DiskScanner _sut = new();

    [Fact]
    public async Task ScanRecursiveAsync_returns_zero_for_empty_directory()
    {
        using var fix = new TempDirectoryFixture();

        var result = await _sut.ScanRecursiveAsync(fix.Root, progress: null, CancellationToken.None);

        Assert.Equal(0, result.SizeBytes);
        Assert.Equal(0, result.FileCount);
        Assert.Empty(result.Children);
        Assert.Empty(result.Files);
    }

    [Fact]
    public async Task ScanRecursiveAsync_aggregates_sizes_across_nested_directories()
    {
        using var fix = new TempDirectoryFixture();
        fix.CreateFile("a.txt", new string('x', 100));
        fix.CreateFile("sub/b.txt", new string('y', 200));
        fix.CreateFile("sub/deep/c.txt", new string('z', 50));

        var result = await _sut.ScanRecursiveAsync(fix.Root, progress: null, CancellationToken.None);

        Assert.Equal(350, result.SizeBytes);
        Assert.Equal(3, result.FileCount);
        Assert.Single(result.Files);
        Assert.Single(result.Children);
        Assert.Equal(250, result.Children[0].SizeBytes);
    }

    [Fact]
    public async Task ScanRecursiveAsync_skips_reparse_points_to_avoid_double_counting()
    {
        using var fix = new TempDirectoryFixture();
        var realFolder = fix.CreateSubDir("real");
        File.WriteAllText(Path.Combine(realFolder, "data.bin"), new string('x', 1_000));

        var separateTarget = fix.CreateSubDir("separate-target");
        File.WriteAllText(Path.Combine(separateTarget, "big.bin"), new string('y', 5_000));

        // Junction inside the scan root → would double-count if we descended.
        fix.CreateJunction(Path.Combine(fix.Root, "link"), separateTarget);

        var result = await _sut.ScanRecursiveAsync(fix.Root, progress: null, CancellationToken.None);

        Assert.Equal(1_000 + 5_000, result.SizeBytes); // separate-target counted once, link skipped.
        Assert.Equal(2, result.FileCount);
    }

    [Fact]
    public async Task ScanRecursiveAsync_returns_files_sorted_into_correct_directory_node()
    {
        using var fix = new TempDirectoryFixture();
        fix.CreateFile("top.txt", "1");
        fix.CreateFile("a/leaf.txt", "12");

        var result = await _sut.ScanRecursiveAsync(fix.Root, progress: null, CancellationToken.None);

        Assert.Single(result.Files);
        Assert.Equal("top.txt", result.Files[0].Name);
        Assert.Single(result.Children);
        Assert.Single(result.Children[0].Files);
        Assert.Equal("leaf.txt", result.Children[0].Files[0].Name);
    }

    [Fact]
    public async Task ScanRecursiveAsync_throws_OperationCanceled_when_token_cancels()
    {
        using var fix = new TempDirectoryFixture();
        for (var i = 0; i < 10; i++)
        {
            fix.CreateFile($"sub/{i}/data.txt", new string('x', 500));
        }
        using var cts = new CancellationTokenSource();
        cts.Cancel();

        await Assert.ThrowsAnyAsync<OperationCanceledException>(
            () => _sut.ScanRecursiveAsync(fix.Root, progress: null, cts.Token));
    }

    [Fact]
    public async Task ScanRecursiveAsync_reports_progress_for_large_scans()
    {
        using var fix = new TempDirectoryFixture();
        // Need > 1000 files to trigger at least one progress event (we batch every 1000).
        for (var i = 0; i < 1_100; i++)
        {
            fix.CreateFile($"sub/file-{i}.txt", "x");
        }

        var events = new List<ScanProgress>();
        var progress = new Progress<ScanProgress>(p =>
        {
            lock (events) { events.Add(p); }
        });

        await _sut.ScanRecursiveAsync(fix.Root, progress, CancellationToken.None);
        await Task.Delay(100); // drain queued progress dispatches

        Assert.NotEmpty(events);
    }
}
