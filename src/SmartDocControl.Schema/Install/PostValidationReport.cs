namespace SmartDocControl.Schema.Install;

public sealed class PostValidationReport
{
    public IReadOnlyList<MissingObject> Missing { get; }
    public int VerifiedCount { get; }
    public bool IsValid => Missing.Count == 0;

    public PostValidationReport(IReadOnlyList<MissingObject> missing, int verifiedCount)
    {
        Missing = missing;
        VerifiedCount = verifiedCount;
    }
}

public sealed record MissingObject
{
    public InstallObjectType ObjectType { get; init; }
    public string ObjectName { get; init; } = string.Empty;
    public string Reason { get; init; } = string.Empty;
}
