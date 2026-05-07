using SmartDocControl.Application.Models;

namespace SmartDocControl.Application.Exceptions;

public sealed class StartupValidationException : Exception
{
    public IReadOnlyList<ValidationIssue> Errors { get; }

    public StartupValidationException(IReadOnlyList<ValidationIssue> errors)
        : base(BuildMessage(errors))
    {
        ArgumentNullException.ThrowIfNull(errors);
        Errors = errors;
    }

    private static string BuildMessage(IReadOnlyList<ValidationIssue>? errors)
    {
        if (errors is null || errors.Count == 0)
            return "Startup validation failed.";

        var lines = errors.Select(e => $"[{e.Code}] {e.Message}");
        return "Startup validation failed:" + Environment.NewLine + string.Join(Environment.NewLine, lines);
    }
}
