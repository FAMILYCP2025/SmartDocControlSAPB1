namespace SmartDocControl.Schema.Install;

public sealed class PostValidationReport
{
    public IReadOnlyList<MissingObject> Missing { get; }
    public int VerifiedCount { get; }
    public int RequiredCount { get; }
    public bool IsValid => Missing.Count == 0;

    public PostValidationReport(IReadOnlyList<MissingObject> missing, int verifiedCount, int requiredCount)
    {
        Missing = missing;
        VerifiedCount = verifiedCount;
        RequiredCount = requiredCount;
    }

    // Backward-compatible overload: RequiredCount derived from verified + missing.
    public PostValidationReport(IReadOnlyList<MissingObject> missing, int verifiedCount)
        : this(missing, verifiedCount, verifiedCount + missing.Count)
    {
    }
}

public sealed record MissingObject
{
    public InstallObjectType ObjectType { get; init; }
    public string ObjectName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
