namespace LinkMover.Core.Abstractions;

public interface IPrivilegeService
{
    bool IsElevated { get; }

    bool CanWriteTo(string path);

    Task<bool> RestartElevatedAsync(string operationToResume);
}
