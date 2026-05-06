using SmartDocControl.Domain.Enums;

namespace SmartDocControl.Domain.ValueObjects;

public sealed record RuleEvaluationResult(CloseDecision Decision, string Reason, int EvaluatedDays);
