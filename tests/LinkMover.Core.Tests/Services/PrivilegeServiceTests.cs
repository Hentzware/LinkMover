using LinkMover.Core.Services;

namespace LinkMover.Core.Tests.Services;

public class PrivilegeServiceTests
{
    private readonly PrivilegeService _sut = new();

    [Fact]
    public void IsElevated_does_not_throw()
    {
        var _ = _sut.IsElevated;
    }

    [Fact]
    public void CanWriteTo_returns_true_for_writable_temp_directory()
    {
        using var fix = new TempDirectoryFixture();

        Assert.True(_sut.CanWriteTo(fix.Root));
    }

    [Fact]
    public void CanWriteTo_returns_false_for_missing_directory()
    {
        var missing = Path.Combine(Path.GetTempPath(), "lm-missing-" + Guid.NewGuid().ToString("N"));

        Assert.False(_sut.CanWriteTo(missing));
    }

    [Fact]
    public void CanWriteTo_returns_false_for_file_path()
    {
        using var fix = new TempDirectoryFixture();
        var filePath = fix.CreateFile("a.txt");

        Assert.False(_sut.CanWriteTo(filePath));
    }
}
