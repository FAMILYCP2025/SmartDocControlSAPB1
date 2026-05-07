namespace SmartDocControl.Application.Models;

public sealed class ExecutionSummary
{
    public DateTime StartedAt { get; }
    public DateTime? FinishedAt { get; set; }
    public int TotalProcessed { get; set; }
    public int TotalClosed { get; set; }
    public int TotalSimulated { get; set; }
    public int TotalSkipped { get; set; }
    public int TotalErrors { get; set; }

    public ExecutionSummary(DateTime startedAt)
    {
        if (startedAt == default)
            throw new ArgumentException("StartedAt is required.", nameof(startedAt));
        StartedAt = startedAt;
    }
}
