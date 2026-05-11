namespace SmartDocControl.Schema.Descriptors;

public sealed record UdtDescriptor
{
    public string Type { get; init; } = string.Empty;
    public string Operation { get; init; } = string.Empty;
    public string TableName { get; init; } = string.Empty;
    public string TableDescription { get; init; } = string.Empty;
    public string TableType { get; init; } = string.Empty;
}
