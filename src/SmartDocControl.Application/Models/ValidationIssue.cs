namespace SmartDocControl.Application.Models;

public enum ValidationSeverity
{
    Error,
    Warning
}

public sealed record ValidationIssue(string Code, ValidationSeverity Severity, string Message);
