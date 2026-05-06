using SmartDocControl.Domain.Enums;
using SmartDocControl.Domain.ValueObjects;

namespace SmartDocControl.Domain.Entities;

public sealed class DocumentRule
{
    public string RuleCode { get; }
    public DocumentType DocumentType { get; }
    public int GraceDays { get; }
    public bool IsActive { get; }
    public bool RequireNoTarget { get; }
    public bool RequireNoRecentActivity { get; }
    public bool RequireApproved { get; }
    public int MaxDocumentsPerRun { get; }

    public DocumentRule(
        string ruleCode,
        DocumentType documentType,
        int graceDays,
        bool isActive,
        bool requireNoTarget = false,
        bool requireNoRecentActivity = false,
        bool requireApproved = false,
        int maxDocumentsPerRun = 0)
    {
        if (string.IsNullOrWhiteSpace(ruleCode))
            throw new ArgumentException("RuleCode is required.", nameof(ruleCode));
        if (graceDays < 0)
            throw new ArgumentOutOfRangeException(nameof(graceDays), "GraceDays cannot be negative.");
        if (!Enum.IsDefined(documentType))
            throw new ArgumentOutOfRangeException(nameof(documentType), "Invalid document type.");

        RuleCode = ruleCode;
        DocumentType = documentType;
        GraceDays = graceDays;
        IsActive = isActive;
        RequireNoTarget = requireNoTarget;
        RequireNoRecentActivity = requireNoRecentActivity;
        RequireApproved = requireApproved;
        MaxDocumentsPerRun = maxDocumentsPerRun;
    }

    public RuleEvaluationResult Evaluate(Document document, DateTime executionDate)
    {
        if (!IsActive)
            return new RuleEvaluationResult(CloseDecision.SkipInactiveRule, "Rule is inactive.", 0);

        if (document.IsClosed)
            return new RuleEvaluationResult(CloseDecision.SkipClosed, "Document is already closed.", 0);

        if (RequireApproved)
            return new RuleEvaluationResult(CloseDecision.SkipApprovalRequired,
                "Approval validation is reserved for PRO version (D04).", 0);

        if (RequireNoTarget && document.HasTargetDocument)
            return new RuleEvaluationResult(CloseDecision.SkipHasTarget,
                "Document has a target document.", 0);

        if (RequireNoRecentActivity && document.HasRecentActivity)
            return new RuleEvaluationResult(CloseDecision.SkipRecentActivity,
                "Document has recent activity.", 0);

        int elapsedDays = (executionDate.Date - document.BaseDate.Date).Days;

        if (elapsedDays < GraceDays)
            return new RuleEvaluationResult(CloseDecision.SkipGracePeriod,
                $"Grace period not exceeded: {elapsedDays} of {GraceDays} days elapsed.", elapsedDays);

        return new RuleEvaluationResult(CloseDecision.Eligible,
            $"Eligible for closing: {elapsedDays} days elapsed, {GraceDays} required.", elapsedDays);
    }
}
