namespace SmartDocControl.Application.Models;

public sealed class StartupValidationReport
{
    public IReadOnlyList<ValidationIssue> Issues { get; }
    public DateTime ValidatedAt { get; }

    public bool IsValid => !Issues.Any(i => i.Severity == ValidationSeverity.Error);

    public IReadOnlyList<ValidationIssue> Errors =>
        Issues.Where(i => i.Severity == ValidationSeverity.Error).ToList().AsReadOnly();

    public IReadOnlyList<ValidationIssue> Warnings =>
        Issues.Where(i => i.Severity == ValidationSeverity.Warning).ToList().AsReadOnly();

    public StartupValidationReport(IReadOnlyList<ValidationIssue> issues)
    {
        ArgumentNullException.ThrowIfNull(issues);
        Issues = issues;
        ValidatedAt = DateTime.UtcNow;
    }
}
