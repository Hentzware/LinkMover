using LinkMover.Core.Services;

namespace LinkMover.Core.Tests.Services;

public class JunctionServiceTests
{
    private readonly JunctionService _sut = new(new FileSystemService());

    [Fact]
    public async Task CreateJunctionAsync_creates_junction_to_target()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var link = Path.Combine(fix.Root, "link");

        var result = await _sut.CreateJunctionAsync(link, target, CancellationToken.None);

        Assert.True(result);
        Assert.True(Directory.Exists(link));
        Assert.True(new FileSystemService().IsReparsePoint(link));
    }

    [Fact]
    public async Task CreateJunctionAsync_throws_when_source_already_exists()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var existing = fix.CreateSubDir("link");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateJunctionAsync(existing, target, CancellationToken.None));
    }

    [Fact]
    public async Task CreateJunctionAsync_throws_when_target_missing()
    {
        using var fix = new TempDirectoryFixture();
        var link = Path.Combine(fix.Root, "link");

        await Assert.ThrowsAsync<InvalidOperationException>(
            () => _sut.CreateJunctionAsync(link, Path.Combine(fix.Root, "missing"), CancellationToken.None));
    }

    [Fact]
    public async Task RemoveJunctionAsync_removes_link_but_keeps_target_contents()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var fileInTarget = Path.Combine(target, "data.txt");
        File.WriteAllText(fileInTarget, "important");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        var result = await _sut.RemoveJunctionAsync(link);

        Assert.True(result);
        Assert.False(Directory.Exists(link));
        Assert.True(File.Exists(fileInTarget));
    }

    [Fact]
    public async Task RemoveJunctionAsync_throws_for_non_reparse_directory()
    {
        using var fix = new TempDirectoryFixture();
        var dir = fix.CreateSubDir("normal");

        await Assert.ThrowsAsync<InvalidOperationException>(() => _sut.RemoveJunctionAsync(dir));
    }

    [Fact]
    public void Inspect_returns_info_for_junction()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        var info = _sut.Inspect(link);

        Assert.NotNull(info);
        Assert.Equal(Path.GetFullPath(link), info!.Source);
        Assert.Equal(Path.GetFullPath(target), Path.GetFullPath(info.Target));
        Assert.True(info.TargetExists);
    }

    [Fact]
    public void Inspect_returns_null_for_normal_directory()
    {
        using var fix = new TempDirectoryFixture();
        var dir = fix.CreateSubDir("normal");

        Assert.Null(_sut.Inspect(dir));
    }

    [Fact]
    public void Inspect_marks_target_missing_when_target_was_deleted()
    {
        using var fix = new TempDirectoryFixture();
        var target = fix.CreateSubDir("target");
        var link = Path.Combine(fix.Root, "link");
        fix.CreateJunction(link, target);

        Directory.Delete(target);

        var info = _sut.Inspect(link);

        Assert.NotNull(info);
        Assert.False(info!.TargetExists);
    }
}
