using SmartDocControl.Application.Models;

namespace SmartDocControl.Application.Ports;

public interface ILogRepository
{
    Task SaveDocumentResultAsync(
        DocumentProcessingResult result,
        RunContext context,
        CancellationToken cancellationToken = default);

    Task SaveExecutionSummaryAsync(
        ExecutionSummary summary,
        RunContext context,
        CancellationToken cancellationToken = default);
}
