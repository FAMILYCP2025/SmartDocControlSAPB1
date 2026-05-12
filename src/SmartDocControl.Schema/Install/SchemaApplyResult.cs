namespace SmartDocControl.Schema.Install;

public sealed class SchemaApplyResult
{
    public IReadOnlyList<SchemaApplyEntryResult> Entries { get; }
    public bool WasAborted { get; }
    public string? AbortReason { get; }
    public bool IsSuccessful { get; }
    public int TotalCreated { get; }
    public int TotalAlreadyExisted { get; }
    public int TotalSkipped { get; }
    public int TotalDryRun { get; }
    public int TotalFailed { get; }
    public int TotalAborted { get; }

    public SchemaApplyResult(
        IReadOnlyList<SchemaApplyEntryResult> entries,
        bool wasAborted = false,
        string? abortReason = null)
    {
        Entries = entries;
        WasAborted = wasAborted;
        AbortReason = abortReason;

        TotalCreated        = entries.Count(e => e.Status == SchemaApplyStatus.Created);
        TotalAlreadyExisted = entries.Count(e => e.Status == SchemaApplyStatus.AlreadyExists);
        TotalSkipped        = entries.Count(e => e.Status == SchemaApplyStatus.Skipped);
        TotalDryRun         = entries.Count(e => e.Status == SchemaApplyStatus.DryRun);
        TotalFailed         = entries.Count(e => e.Status == SchemaApplyStatus.Failed);
        TotalAborted        = entries.Count(e => e.Status == SchemaApplyStatus.Aborted);

        IsSuccessful = !wasAborted && TotalFailed == 0 && TotalAborted == 0;
    }
}
