using System.Text.Json.Serialization;

namespace SmartDocControl.Schema.Sap.Dtos;

internal sealed class SapUserTablePayload
{
    [JsonPropertyName("TableName")] public string TableName { get; set; } = string.Empty;
    [JsonPropertyName("TableDescription")] public string TableDescription { get; set; } = string.Empty;
    [JsonPropertyName("TableType")] public string TableType { get; set; } = string.Empty;
}

internal sealed class SapUserFieldPayload
{
    [JsonPropertyName("TableName")] public string TableName { get; set; } = string.Empty;
    [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;
    [JsonPropertyName("Type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("EditSize")] public int? EditSize { get; set; }
    [JsonPropertyName("Mandatory")] public string? Mandatory { get; set; }
    [JsonPropertyName("DefaultValue")] public string? DefaultValue { get; set; }
    [JsonPropertyName("ValidValuesMD")] public List<SapValidValuePayload>? ValidValuesMD { get; set; }
}

internal sealed class SapValidValuePayload
{
    [JsonPropertyName("Value")] public string Value { get; set; } = string.Empty;
    [JsonPropertyName("Description")] public string Description { get; set; } = string.Empty;
}

internal sealed class SapTableListResponse
{
    [JsonPropertyName("value")] public List<SapTableEntry>? Value { get; set; }
}

internal sealed class SapTableEntry
{
    [JsonPropertyName("TableName")] public string TableName { get; set; } = string.Empty;
    [JsonPropertyName("TableType")] public string TableType { get; set; } = string.Empty;
}

internal sealed class SapFieldListResponse
{
    [JsonPropertyName("value")] public List<SapFieldEntry>? Value { get; set; }
}

internal sealed class SapFieldEntry
{
    [JsonPropertyName("TableName")] public string TableName { get; set; } = string.Empty;
    [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
    [JsonPropertyName("FieldID")] public int FieldID { get; set; }
    [JsonPropertyName("Type")] public string Type { get; set; } = string.Empty;
    [JsonPropertyName("EditSize")] public int EditSize { get; set; }
}

internal sealed class SapErrorEnvelope
{
    [JsonPropertyName("error")] public SapErrorBody? Error { get; set; }
}

internal sealed class SapErrorBody
{
    [JsonPropertyName("code")] public object? Code { get; set; }
    [JsonPropertyName("message")] public SapErrorMessage? Message { get; set; }
}

internal sealed class SapErrorMessage
{
    [JsonPropertyName("lang")] public string? Lang { get; set; }
    [JsonPropertyName("value")] public string? Value { get; set; }
}
