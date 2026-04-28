// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using System.IO;
using LinkMover.Core.Services;
using LinkMover.Inventory.Repoint;

namespace LinkMover.Inventory.Tests.Repoint;

public class JunctionRepointOrchestratorTests
{
    private readonly JunctionRepointOrchestrator _sut;
    private readonly FileSystemService _fs;
    private readonly JunctionService _junctions;

    public JunctionRepointOrchestratorTests()
    {
        _fs = new FileSystemService();
        _junctions = new JunctionService(_fs);
        _sut = new JunctionRepointOrchestrator(_fs, _junctions, new RobocopyService(), new VerificationService(_fs));
    }

    [Fact]
    public async Task RepointAsync_moves_data_and_repoints_junction_to_new_target()
    {
        using var fix = new TempDirectoryFixture();
        var oldTarget = fix.CreateSubDir("old-target");
        File.WriteAllText(Path.Combine(oldTarget, "data.txt"), "important");
        Directory.CreateDirectory(Path.Combine(oldTarget, "sub"));
        File.WriteAllText(Path.Combine(oldTarget, "sub", "more.txt"), "more");

        var source = Path.Combine(fix.Root, "link");
        fix.CreateJunction(source, oldTarget);

        var newTarget = Path.Combine(fix.Root, "new-target");

        var result = await _sut.RepointAsync(source, oldTarget, newTarget,
            deleteOldTargetAfter: false, progress: null, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.Equal(2, result.FilesCopied);
        Assert.True(_fs.IsReparsePoint(source));
        Assert.Equal(Path.GetFullPath(newTarget), _fs.ResolveLinkTarget(source));
        Assert.True(File.Exists(Path.Combine(newTarget, "data.txt")));
        Assert.True(File.Exists(Path.Combine(newTarget, "sub", "more.txt")));
        Assert.True(Directory.Exists(oldTarget)); // not deleted
    }

    [Fact]
    public async Task RepointAsync_deletes_old_target_when_requested()
    {
        using var fix = new TempDirectoryFixture();
        var oldTarget = fix.CreateSubDir("old-target");
        File.WriteAllText(Path.Combine(oldTarget, "data.txt"), "x");
        var source = Path.Combine(fix.Root, "link");
        fix.CreateJunction(source, oldTarget);
        var newTarget = Path.Combine(fix.Root, "new-target");

        var result = await _sut.RepointAsync(source, oldTarget, newTarget,
            deleteOldTargetAfter: true, progress: null, CancellationToken.None);

        Assert.True(result.Success, result.ErrorMessage);
        Assert.False(Directory.Exists(oldTarget));
    }

    [Fact]
    public async Task RepointAsync_fails_when_source_is_not_a_junction()
    {
        using var fix = new TempDirectoryFixture();
        var realDir = fix.CreateSubDir("real");
        var oldTarget = fix.CreateSubDir("old");
        var newTarget = Path.Combine(fix.Root, "new");

        var result = await _sut.RepointAsync(realDir, oldTarget, newTarget,
            deleteOldTargetAfter: false, progress: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("Reparse", result.ErrorMessage);
    }

    [Fact]
    public async Task RepointAsync_fails_when_new_target_already_has_content()
    {
        using var fix = new TempDirectoryFixture();
        var oldTarget = fix.CreateSubDir("old-target");
        File.WriteAllText(Path.Combine(oldTarget, "x.txt"), "x");
        var source = Path.Combine(fix.Root, "link");
        fix.CreateJunction(source, oldTarget);
        var newTarget = fix.CreateSubDir("new-target");
        File.WriteAllText(Path.Combine(newTarget, "stale.txt"), "stale");

        var result = await _sut.RepointAsync(source, oldTarget, newTarget,
            deleteOldTargetAfter: false, progress: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("nicht leer", result.ErrorMessage);
        // Old junction still intact, no destruction happened
        Assert.True(_fs.IsReparsePoint(source));
        Assert.Equal(Path.GetFullPath(oldTarget), _fs.ResolveLinkTarget(source));
    }

    [Fact]
    public async Task RepointAsync_fails_when_new_target_equals_old_target()
    {
        using var fix = new TempDirectoryFixture();
        var oldTarget = fix.CreateSubDir("target");
        var source = Path.Combine(fix.Root, "link");
        fix.CreateJunction(source, oldTarget);

        var result = await _sut.RepointAsync(source, oldTarget, oldTarget,
            deleteOldTargetAfter: false, progress: null, CancellationToken.None);

        Assert.False(result.Success);
        Assert.Contains("identisch", result.ErrorMessage);
    }

    [Fact]
    public async Task RepointAsync_reports_progress_through_stages()
    {
        using var fix = new TempDirectoryFixture();
        var oldTarget = fix.CreateSubDir("old");
        File.WriteAllText(Path.Combine(oldTarget, "x.txt"), "x");
        var source = Path.Combine(fix.Root, "link");
        fix.CreateJunction(source, oldTarget);
        var newTarget = Path.Combine(fix.Root, "new");

        var stages = new List<RepointStage>();
        var progress = new Progress<RepointProgress>(p =>
        {
            lock (stages)
            {
                if (stages.Count == 0 || stages[^1] != p.Stage) stages.Add(p.Stage);
            }
        });

        await _sut.RepointAsync(source, oldTarget, newTarget,
            deleteOldTargetAfter: true, progress, CancellationToken.None);
        await Task.Delay(100);

        Assert.Contains(RepointStage.PreFlight, stages);
        Assert.Contains(RepointStage.Copying, stages);
        Assert.Contains(RepointStage.RemovingOldJunction, stages);
        Assert.Contains(RepointStage.CreatingNewJunction, stages);
        Assert.Contains(RepointStage.DeletingOldTarget, stages);
        Assert.Contains(RepointStage.Done, stages);
    }
}
