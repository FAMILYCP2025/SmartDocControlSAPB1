using System.Text.Json.Serialization;

namespace SmartDocControl.Infrastructure.ServiceLayer.Dtos;

internal sealed class SapRuleDto
{
    [JsonPropertyName("Code")]
    public string Code { get; set; } = string.Empty;

    [JsonPropertyName("U_Active")]
    public string? Active { get; set; }

    [JsonPropertyName("U_EntitySet")]
    public string? EntitySet { get; set; }

    [JsonPropertyName("U_GraceDays")]
    public int GraceDays { get; set; }

    [JsonPropertyName("U_OnlyNoTarget")]
    public string? OnlyNoTarget { get; set; }

    [JsonPropertyName("U_CheckUpdate")]
    public string? CheckUpdate { get; set; }

    [JsonPropertyName("U_ReqApproval")]
    public string? ReqApproval { get; set; }

    [JsonPropertyName("U_MaxPerRun")]
    public int MaxPerRun { get; set; }
}
