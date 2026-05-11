namespace SmartDocControl.Schema.Descriptors;

public sealed record UdfDescriptor
{
    public string TableName { get; init; } = string.Empty;
    public string Name { get; init; } = string.Empty;
    public string FieldDescription { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int? Size { get; init; }
    public string? DefaultValue { get; init; }
    public bool? Mandatory { get; init; }
    public IReadOnlyList<ValidValueDescriptor>? ValidValues { get; init; }
}
