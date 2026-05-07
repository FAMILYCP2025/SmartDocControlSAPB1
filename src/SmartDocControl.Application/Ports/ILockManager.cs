namespace SmartDocControl.Application.Ports;

public interface ILockManager
{
    Task<bool> AcquireAsync(string runId, CancellationToken cancellationToken = default);
    Task ReleaseAsync(string runId, CancellationToken cancellationToken = default);
}
