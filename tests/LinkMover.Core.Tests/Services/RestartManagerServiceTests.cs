using System.Diagnostics;
using LinkMover.Core.Services;

namespace LinkMover.Core.Tests.Services;

public class RestartManagerServiceTests
{
    private readonly RestartManagerService _sut = new();

    [Fact]
    public async Task GetLockingProcessesAsync_returns_empty_for_empty_input()
    {
        var result = await _sut.GetLockingProcessesAsync(Array.Empty<string>(), CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLockingProcessesAsync_returns_empty_for_unlocked_file()
    {
        using var fix = new TempDirectoryFixture();
        var file = fix.CreateFile("idle.bin", "no lock");

        var result = await _sut.GetLockingProcessesAsync(new[] { file }, CancellationToken.None);

        Assert.Empty(result);
    }

    [Fact]
    public async Task GetLockingProcessesAsync_finds_current_process_for_locked_file()
    {
        using var fix = new TempDirectoryFixture();
        var file = Path.Combine(fix.Root, "locked.bin");

        await using var stream = new FileStream(
            file,
            FileMode.CreateNew,
            FileAccess.ReadWrite,
            FileShare.None);

        var result = await _sut.GetLockingProcessesAsync(new[] { file }, CancellationToken.None);

        var currentPid = Process.GetCurrentProcess().Id;
        Assert.Contains(result, p => p.ProcessId == currentPid);
    }

    [Fact]
    public async Task EndProcessesAsync_returns_true_for_empty_list()
    {
        var result = await _sut.EndProcessesAsync(Array.Empty<int>(), CancellationToken.None);

        Assert.True(result);
    }
}
