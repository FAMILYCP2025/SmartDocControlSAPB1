using System.Text.Json.Serialization;

namespace SmartDocControl.Infrastructure.ServiceLayer.Dtos;

internal sealed class SapPagedResult<T>
{
    [JsonPropertyName("value")]
    public List<T> Value { get; set; } = new();
}
