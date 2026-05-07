using System.Text.Json.Serialization;

namespace SmartDocControl.Infrastructure.ServiceLayer.Dtos;

internal sealed class SapUserTableDto
{
    [JsonPropertyName("TableName")]
    public string TableName { get; set; } = string.Empty;
}
