using SmartDocControl.Domain.Enums;

namespace SmartDocControl.Domain.Entities;

public sealed class ExecutionResult
{
    public Document Document { get; }
    public CloseDecision Decision { get; }
    public ExecutionStatus Status { get; }
    public string Message { get; }
    public DateTime ExecutedAt { get; }

    public ExecutionResult(
        Document document,
        CloseDecision decision,
        ExecutionStatus status,
        string message,
        DateTime executedAt)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        Decision = decision;
        Status = status;
        Message = message ?? string.Empty;
        ExecutedAt = executedAt;
    }
}
