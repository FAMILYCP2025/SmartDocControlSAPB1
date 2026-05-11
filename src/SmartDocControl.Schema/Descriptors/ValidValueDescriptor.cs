namespace SmartDocControl.Schema.Descriptors;

public sealed record ValidValueDescriptor
{
    public string Value { get; init; } = string.Empty;
    public string Description { get; init; } = string.Empty;
}
