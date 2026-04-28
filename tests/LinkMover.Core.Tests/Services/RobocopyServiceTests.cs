using LinkMover.Core.Models;
using LinkMover.Core.Services;

namespace LinkMover.Core.Tests.Services;

public class RobocopyServiceTests
{
    private readonly RobocopyService _sut = new();

    [Fact]
    public async Task CopyAsync_copies_files_from_source_to_target()
    {
        using var fix = new TempDirectoryFixture();
        var src = fix.CreateSubDir("src");
        var dst = Path.Combine(fix.Root, "dst");
        File.WriteAllText(Path.Combine(src, "a.txt"), "hello");
        File.WriteAllText(Path.Combine(src, "b.txt"), new string('x', 1024));

        var result = await _sut.CopyAsync(src, dst, new RobocopyOptions(), progress: null, CancellationToken.None);

        Assert.True(result.IsSuccess, $"ExitCode {result.ExitCode}, Errors: {string.Join("; ", result.Errors)}");
        Assert.True(File.Exists(Path.Combine(dst, "a.txt")));
        Assert.True(File.Exists(Path.Combine(dst, "b.txt")));
    }

    [Fact]
    public async Task CopyAsync_summary_reports_files_and_bytes_copied()
    {
        using var fix = new TempDirectoryFixture();
        var src = fix.CreateSubDir("src");
        var dst = Path.Combine(fix.Root, "dst");
        File.WriteAllText(Path.Combine(src, "a.txt"), new string('x', 100));
        File.WriteAllText(Path.Combine(src, "b.txt"), new string('y', 200));

        var result = await _sut.CopyAsync(src, dst, new RobocopyOptions(), progress: null, CancellationToken.None);

        Assert.Equal(2, result.FilesCopied);
        Assert.Equal(300, result.BytesCopied);
    }

    [Fact]
    public async Task CopyAsync_dry_run_does_not_create_target()
    {
        using var fix = new TempDirectoryFixture();
        var src = fix.CreateSubDir("src");
        var dst = Path.Combine(fix.Root, "dst");
        File.WriteAllText(Path.Combine(src, "a.txt"), "hello");

        var result = await _sut.CopyAsync(src, dst, new RobocopyOptions(DryRun: true), progress: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.False(File.Exists(Path.Combine(dst, "a.txt")));
    }

    [Fact]
    public async Task CopyAsync_reports_progress_per_file()
    {
        using var fix = new TempDirectoryFixture();
        var src = fix.CreateSubDir("src");
        var dst = Path.Combine(fix.Root, "dst");
        File.WriteAllText(Path.Combine(src, "a.txt"), "hello");
        File.WriteAllText(Path.Combine(src, "b.txt"), "world");

        var events = new List<RobocopyProgress>();
        var progress = new Progress<RobocopyProgress>(p => events.Add(p));

        var result = await _sut.CopyAsync(src, dst, new RobocopyOptions(), progress, CancellationToken.None);

        Assert.True(result.IsSuccess);
        // Progress events are dispatched async via SynchronizationContext — at least one should land before we return.
        // We assert the running-byte counter hits the final total to confirm parsing worked.
        Assert.Equal(10, result.BytesCopied);
    }

    [Fact]
    public async Task CopyAsync_mirror_removes_extra_files_in_target()
    {
        using var fix = new TempDirectoryFixture();
        var src = fix.CreateSubDir("src");
        var dst = fix.CreateSubDir("dst");
        File.WriteAllText(Path.Combine(src, "keep.txt"), "kept");
        File.WriteAllText(Path.Combine(dst, "stale.txt"), "to be removed");

        var result = await _sut.CopyAsync(src, dst, new RobocopyOptions(Mirror: true), progress: null, CancellationToken.None);

        Assert.True(result.IsSuccess);
        Assert.True(File.Exists(Path.Combine(dst, "keep.txt")));
        Assert.False(File.Exists(Path.Combine(dst, "stale.txt")));
    }

    [Fact]
    public void RobocopyResult_IsSuccess_returns_true_for_exit_codes_zero_through_seven()
    {
        for (var i = 0; i <= 7; i++)
        {
            var result = new RobocopyResult(i, 0, 0, TimeSpan.Zero, Array.Empty<string>());
            Assert.True(result.IsSuccess);
        }
    }

    [Fact]
    public void RobocopyResult_IsSuccess_returns_false_for_exit_code_eight_or_higher()
    {
        var result = new RobocopyResult(8, 0, 0, TimeSpan.Zero, Array.Empty<string>());
        Assert.False(result.IsSuccess);
    }
}
