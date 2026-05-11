namespace SmartDocControl.Schema.Descriptors;

public sealed record SchemaManifest
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string AppVersionMinimum { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
    public IReadOnlyList<string> Steps { get; init; } = Array.Empty<string>();
}
