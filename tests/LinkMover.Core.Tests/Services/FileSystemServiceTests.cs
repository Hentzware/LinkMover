// Copyright (c) 2026 Henning Entz
// SPDX-License-Identifier: Apache-2.0 WITH LicenseRef-Commons-Clause
// See LICENSE for the full Apache 2.0 text and Commons Clause restriction.

using LinkMover.Core.Services;

namespace LinkMover.Core.Tests.Services;

public class FileSystemServiceTests
{
    private readonly FileSystemService _sut = new();

    [Fact]
    public void IsReparsePoint_returns_false_for_normal_directory()
    {
        using var fix = new TempDirectoryFixture();
        var dir = fix.CreateSubDir("normal");

        Assert.False(_sut.IsReparsePoint(dir));
    }

    [Fact]
    public void IsReparsePoint_returns_true_for_junction()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        Assert.True(_sut.IsReparsePoint(link));
    }

    [Fact]
    public void ResolveLinkTarget_returns_target_for_junction()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        var resolved = _sut.ResolveLinkTarget(link);

        Assert.NotNull(resolved);
        Assert.Equal(Path.GetFullPath(target), Path.GetFullPath(resolved!));
    }

    [Fact]
    public async Task GetDirectorySizeAsync_sums_file_bytes()
    {
        using var fix = new TempDirectoryFixture();
        fix.CreateFile("a.txt", new string('x', 100));
        fix.CreateFile("sub/b.txt", new string('y', 250));

        var size = await _sut.GetDirectorySizeAsync(fix.Root, progress: null, CancellationToken.None);

        Assert.Equal(350, size);
    }

    [Fact]
    public async Task GetDirectorySizeAsync_skips_reparse_points()
    {
        using var fix = new TempDirectoryFixture();
        var content = fix.CreateSubDir("content");
        File.WriteAllText(Path.Combine(content, "big.bin"), new string('x', 1_000));

        var realFolder = fix.CreateSubDir("real");
        File.WriteAllText(Path.Combine(realFolder, "small.bin"), new string('x', 50));

        // Junction inside the scanned root pointing at a populated target.
        // Without skipping reparse points, we'd double-count the 1000 bytes.
        fix.CreateJunction(Path.Combine(fix.Root, "linked"), content);

        var size = await _sut.GetDirectorySizeAsync(fix.Root, progress: null, CancellationToken.None);

        Assert.Equal(1_050, size);
    }

    [Fact]
    public async Task CountFilesAsync_returns_recursive_count()
    {
        using var fix = new TempDirectoryFixture();
        fix.CreateFile("a.txt");
        fix.CreateFile("sub/b.txt");
        fix.CreateFile("sub/deep/c.txt");

        var count = await _sut.CountFilesAsync(fix.Root, CancellationToken.None);

        Assert.Equal(3, count);
    }

    [Fact]
    public void EnumerateReparsePoints_finds_junctions_and_does_not_traverse_into_them()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        fix.CreateFile("target/file.txt");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        var found = _sut.EnumerateReparsePoints(fix.Root, CancellationToken.None).ToList();

        Assert.Single(found);
        Assert.Equal(Path.GetFullPath(link), Path.GetFullPath(found[0]));
    }

    [Fact]
    public void GetDrive_returns_snapshot_for_temp_drive()
    {
        var snapshot = _sut.GetDrive(Path.GetTempPath());

        Assert.NotEmpty(snapshot.Root);
        Assert.True(snapshot.TotalBytes > 0);
        Assert.NotEmpty(snapshot.FileSystem);
    }
}
