using SmartDocControl.Application.Models;
using SmartDocControl.Domain.Entities;
using SmartDocControl.Domain.Enums;

namespace SmartDocControl.Application.Ports;

public interface IConfigurationRepository
{
    Task<IReadOnlyList<DocumentRule>> GetActiveRulesAsync(
        DocumentType? filter = null,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<Exclusion>> GetExclusionsAsync(
        CancellationToken cancellationToken = default);
}
