using SmartDocControl.Application.Models;
using SmartDocControl.Domain.Entities;

namespace SmartDocControl.Application.Ports;

public interface IDocumentRepository
{
    Task<IReadOnlyList<Document>> GetOpenDocumentsAsync(
        DocumentRule rule,
        RunContext context,
        CancellationToken cancellationToken = default);
}
