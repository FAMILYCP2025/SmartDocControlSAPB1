namespace SmartDocControl.Schema.Install;

public sealed class InstallPlan
{
    public IReadOnlyList<InstallPlanEntry> Entries { get; }
    public bool HasBlockingIssues { get; }
    public int TotalCreates { get; }
    public int TotalSkips { get; }
    public int TotalDrifts { get; }

    public InstallPlan(IReadOnlyList<InstallPlanEntry> entries)
    {
        Entries = entries;
        TotalCreates = entries.Count(e => e.Action == InstallAction.Create);
        TotalSkips   = entries.Count(e => e.Action == InstallAction.Skip);
        TotalDrifts  = entries.Count(e => e.Action == InstallAction.Drift);
        HasBlockingIssues = entries.Any(e => e.IsBlocking);
    }
}
