using System.Text.Json.Serialization;

namespace SmartDocControl.Infrastructure.ServiceLayer.Dtos;

internal sealed class SapLoginRequest
{
    [JsonPropertyName("CompanyDB")]
    public string CompanyDB { get; set; } = string.Empty;

    [JsonPropertyName("UserName")]
    public string UserName { get; set; } = string.Empty;

    [JsonPropertyName("Password")]
    public string Password { get; set; } = string.Empty;
}
