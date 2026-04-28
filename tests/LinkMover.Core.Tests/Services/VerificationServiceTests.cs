using LinkMover.Core.Models;
using LinkMover.Core.Services;

namespace LinkMover.Core.Tests.Services;

public class VerificationServiceTests
{
    private readonly VerificationService _sut = new(new FileSystemService());

    [Fact]
    public async Task VerifyAsync_returns_match_for_identical_directories()
    {
        using var fix = new TempDirectoryFixture();
        var src = fix.CreateSubDir("src");
        var dst = fix.CreateSubDir("dst");
        File.WriteAllText(Path.Combine(src, "a.txt"), "hello");
        File.WriteAllText(Path.Combine(dst, "a.txt"), "hello");

        var result = await _sut.VerifyAsync(src, dst, VerifyMode.FileCountAndSize, CancellationToken.None);

        Assert.True(result.Match);
        Assert.Equal(1, result.SourceFileCount);
        Assert.Equal(1, result.TargetFileCount);
        Assert.Equal(5, result.SourceBytes);
        Assert.Equal(5, result.TargetBytes);
        Assert.Empty(result.MissingFiles);
    }

    [Fact]
    public async Task VerifyAsync_reports_missing_files_in_target()
    {
        using var fix = new TempDirectoryFixture();
        var src = fix.CreateSubDir("src");
        var dst = fix.CreateSubDir("dst");
        File.WriteAllText(Path.Combine(src, "a.txt"), "hello");
        Directory.CreateDirectory(Path.Combine(src, "sub"));
        File.WriteAllText(Path.Combine(src, "sub", "b.txt"), "world");
        File.WriteAllText(Path.Combine(dst, "a.txt"), "hello");

        var result = await _sut.VerifyAsync(src, dst, VerifyMode.FileCount, CancellationToken.None);

        Assert.False(result.Match);
        Assert.Contains(result.MissingFiles, p => p.EndsWith("b.txt"));
    }

    [Fact]
    public async Task VerifyAsync_with_RobocopyExitCodeOnly_returns_match_without_inspecting_filesystem()
    {
        using var fix = new TempDirectoryFixture();
        var src = fix.CreateSubDir("src");
        File.WriteAllText(Path.Combine(src, "only-in-src.txt"), "x");
        var dst = fix.CreateSubDir("dst");

        var result = await _sut.VerifyAsync(src, dst, VerifyMode.RobocopyExitCodeOnly, CancellationToken.None);

        Assert.True(result.Match);
    }

    [Fact]
    public async Task VerifyAsync_with_FileCount_ignores_byte_differences_when_files_match()
    {
        using var fix = new TempDirectoryFixture();
        var src = fix.CreateSubDir("src");
        var dst = fix.CreateSubDir("dst");
        File.WriteAllText(Path.Combine(src, "a.txt"), "short");
        File.WriteAllText(Path.Combine(dst, "a.txt"), new string('x', 9999));

        var result = await _sut.VerifyAsync(src, dst, VerifyMode.FileCount, CancellationToken.None);

        Assert.True(result.Match);
    }

    [Fact]
    public async Task VerifyAsync_with_FileCountAndSize_detects_byte_mismatch()
    {
        using var fix = new TempDirectoryFixture();
        var src = fix.CreateSubDir("src");
        var dst = fix.CreateSubDir("dst");
        File.WriteAllText(Path.Combine(src, "a.txt"), "short");
        File.WriteAllText(Path.Combine(dst, "a.txt"), new string('x', 9999));

        var result = await _sut.VerifyAsync(src, dst, VerifyMode.FileCountAndSize, CancellationToken.None);

        Assert.False(result.Match);
        Assert.NotEqual(result.SourceBytes, result.TargetBytes);
    }
}
