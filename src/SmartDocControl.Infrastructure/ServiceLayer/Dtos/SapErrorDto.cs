using System.Text.Json.Serialization;

namespace SmartDocControl.Infrastructure.ServiceLayer.Dtos;

internal sealed class SapErrorEnvelopeDto
{
    [JsonPropertyName("error")]
    public SapErrorDto? Error { get; set; }
}

internal sealed class SapErrorDto
{
    [JsonPropertyName("code")]
    public string? Code { get; set; }

    [JsonPropertyName("message")]
    public SapErrorMessageDto? Message { get; set; }
}

internal sealed class SapErrorMessageDto
{
    [JsonPropertyName("value")]
    public string? Value { get; set; }
}
