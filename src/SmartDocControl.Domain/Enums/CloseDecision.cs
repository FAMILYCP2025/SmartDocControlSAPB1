namespace SmartDocControl.Domain.Enums;

public enum CloseDecision
{
    Eligible,
    SkipClosed,
    SkipHasTarget,
    SkipRecentActivity,
    SkipApprovalRequired,
    SkipInactiveRule,
    SkipGracePeriod
}
