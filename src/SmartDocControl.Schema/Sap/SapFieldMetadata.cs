namespace SmartDocControl.Schema.Sap;

public sealed record SapFieldMetadata
{
    public string TableName { get; init; } = string.Empty;
    public string FieldName { get; init; } = string.Empty;
    public string Type { get; init; } = string.Empty;
    public int Size { get; init; }
}
