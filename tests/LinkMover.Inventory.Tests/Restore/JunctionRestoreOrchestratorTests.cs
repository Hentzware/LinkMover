// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.IO;
using LinkMover.Core.Services;
using LinkMover.Inventory.Restore;

namespace LinkMover.Inventory.Tests.Restore;

public class JunctionRestoreOrchestratorTests
{
    private readonly JunctionRestoreOrchestrator _sut;

    public JunctionRestoreOrchestratorTests()
    {
        var fs = new FileSystemService();
        _sut = new JunctionRestoreOrchestrator(
            fs,
            new JunctionService(fs),
            new RobocopyService(),
            new VerificationService(fs));
    }

    [Fact]
    public async Task RestoreAsync_turns_junction_back_into_real_directory_with_data()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        File.WriteAllText(Path.Combine(target, "data.txt"), "important content");
        Directory.CreateDirectory(Path.Combine(target, "sub"));
        File.WriteAllText(Path.Combine(target, "sub", "more.txt"), "more content");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        var result = await _sut.RestoreAsync(link, target, deleteTargetAfter: false, progress: null, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, result.FilesRestored);
        Assert.False(new FileSystemService().IsReparsePoint(link));
        Assert.True(File.Exists(Path.Combine(link, "data.txt")));
        Assert.Equal("important content", File.ReadAllText(Path.Combine(link, "data.txt")));
        Assert.True(File.Exists(Path.Combine(link, "sub", "more.txt")));
        Assert.True(Directory.Exists(target));
    }

    [Fact]
    public async Task RestoreAsync_deletes_target_when_requested()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        File.WriteAllText(Path.Combine(target, "data.txt"), "x");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        var result = await _sut.RestoreAsync(link, target, deleteTargetAfter: true, progress: null, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.True(File.Exists(Path.Combine(link, "data.txt")));
        Assert.False(Directory.Exists(target));
    }

    [Fact]
    public async Task RestoreAsync_keeps_target_when_not_requested()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        File.WriteAllText(Path.Combine(target, "data.txt"), "x");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        await _sut.RestoreAsync(link, target, deleteTargetAfter: false, progress: null, CancellationToken.None);

        Assert.True(Directory.Exists(target));
        Assert.True(File.Exists(Path.Combine(target, "data.txt")));
    }

    [Fact]
    public async Task RestoreAsync_fails_when_source_is_not_a_junction()
    {
        using var fix = new TempDirectoryFixture();
        var realDir = fix.CreateSubDir("real");
        var target = fix.CreateSubDir("target");

        var result = await _sut.RestoreAsync(realDir, target, deleteTargetAfter: false, progress: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Reparse", result.ErrorMessage);
    }

    [Fact]
    public async Task RestoreAsync_fails_when_target_is_missing()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);
        Directory.Delete(target);

        var result = await _sut.RestoreAsync(link, target, deleteTargetAfter: false, progress: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Ziel-Verzeichnis fehlt", result.ErrorMessage);
    }

    [Fact]
    public async Task RestoreAsync_reports_progress_through_stages()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        File.WriteAllText(Path.Combine(target, "a.txt"), "x");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        var stages = new List<RestoreStage>();
        var progress = new Progress<RestoreProgress>(p =>
        {
            lock (stages)
            {
                if (stages.Count == 0 || stages[^1] != p.Stage)
                {
                    stages.Add(p.Stage);
                }
            }
        });

        var result = await _sut.RestoreAsync(link, target, deleteTargetAfter: true, progress, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        // Progress<T> is async-dispatched, so wait briefly for queued reports.
        await Task.Delay(100);
        Assert.Contains(RestoreStage.PreFlight, stages);
        Assert.Contains(RestoreStage.Staging, stages);
        Assert.Contains(RestoreStage.RemovingJunction, stages);
        Assert.Contains(RestoreStage.MovingStaging, stages);
        Assert.Contains(RestoreStage.DeletingTarget, stages);
        Assert.Contains(RestoreStage.Done, stages);
    }
}
