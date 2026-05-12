namespace SmartDocControl.Schema.Install;

public sealed record SchemaApplyEntryResult
{
    public InstallObjectType ObjectType { get; init; }
    public string ObjectName { get; init; } = string.Empty;
    public SchemaApplyStatus Status { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? ErrorCode { get; init; }
}
