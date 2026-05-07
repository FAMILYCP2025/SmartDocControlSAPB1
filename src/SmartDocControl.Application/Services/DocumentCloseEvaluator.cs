using SmartDocControl.Application.Models;
using SmartDocControl.Domain.Entities;
using SmartDocControl.Domain.Enums;

namespace SmartDocControl.Application.Services;

public sealed class DocumentCloseEvaluator
{
    public DocumentProcessingResult Evaluate(
        Document document,
        DocumentRule rule,
        RunContext context,
        DateTime? processedAt = null)
    {
        ArgumentNullException.ThrowIfNull(document);
        ArgumentNullException.ThrowIfNull(rule);
        ArgumentNullException.ThrowIfNull(context);

        var evaluation = rule.Evaluate(document, context.ExecutionDate);
        var timestamp = processedAt ?? DateTime.UtcNow;

        if (evaluation.Decision != CloseDecision.Eligible)
            return new DocumentProcessingResult(
                document, evaluation,
                ExecutionStatus.Skipped, evaluation.Reason,
                eligibleForClose: false, timestamp);

        if (context.SimulationMode)
            return new DocumentProcessingResult(
                document, evaluation,
                ExecutionStatus.Simulated,
                "Document eligible for closing but execution is in simulation mode.",
                eligibleForClose: true, timestamp);

        return new DocumentProcessingResult(
            document, evaluation,
            ExecutionStatus.Pending,
            "Document eligible for closing.",
            eligibleForClose: true, timestamp);
    }
}
