using SmartDocControl.Domain.Entities;
using SmartDocControl.Domain.Enums;
using SmartDocControl.Domain.ValueObjects;

namespace SmartDocControl.Application.Models;

public sealed class DocumentProcessingResult
{
    public Document Document { get; }
    public RuleEvaluationResult EvaluationResult { get; }
    public ExecutionStatus Status { get; }
    public string Message { get; }
    public bool EligibleForClose { get; }
    public DateTime ProcessedAt { get; }

    public DocumentProcessingResult(
        Document document,
        RuleEvaluationResult evaluationResult,
        ExecutionStatus status,
        string message,
        bool eligibleForClose,
        DateTime processedAt)
    {
        Document = document ?? throw new ArgumentNullException(nameof(document));
        EvaluationResult = evaluationResult ?? throw new ArgumentNullException(nameof(evaluationResult));
        Status = status;
        Message = message ?? string.Empty;
        EligibleForClose = eligibleForClose;
        ProcessedAt = processedAt;
    }
}
