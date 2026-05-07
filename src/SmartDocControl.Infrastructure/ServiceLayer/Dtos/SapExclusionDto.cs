using System.Text.Json.Serialization;

namespace SmartDocControl.Infrastructure.ServiceLayer.Dtos;

internal sealed class SapExclusionDto
{
    [JsonPropertyName("Code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("U_Active")]
    public string? Active { get; set; }

    [JsonPropertyName("U_ObjType")]
    public string? ObjType { get; set; }

    [JsonPropertyName("U_ExcType")]
    public string? ExcType { get; set; }

    [JsonPropertyName("U_ExcValue")]
    public string? ExcValue { get; set; }

    [JsonPropertyName("U_Reason")]
    public string? Reason { get; set; }
}
