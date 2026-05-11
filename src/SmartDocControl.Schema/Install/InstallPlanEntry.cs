namespace SmartDocControl.Schema.Install;

public sealed record InstallPlanEntry
{
    public InstallObjectType ObjectType { get; init; }
    public string ObjectName { get; init; } = string.Empty;
    public InstallAction Action { get; init; }
    public string Reason { get; init; } = string.Empty;
    public bool IsBlocking { get; init; }
}
