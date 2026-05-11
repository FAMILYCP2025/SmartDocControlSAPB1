namespace SmartDocControl.Schema.Sap;

public sealed record SapTableMetadata
{
    public string TableName { get; init; } = string.Empty;
    public string TableType { get; init; } = string.Empty;
}
