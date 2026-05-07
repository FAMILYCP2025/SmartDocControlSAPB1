using SmartDocControl.Application.Models;

namespace SmartDocControl.Application.Ports;

public interface IStartupValidator
{
    Task<StartupValidationReport> ValidateAsync(CancellationToken cancellationToken = default);
}
