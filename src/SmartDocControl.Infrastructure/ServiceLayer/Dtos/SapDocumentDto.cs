using System.Text.Json.Serialization;

namespace SmartDocControl.Infrastructure.ServiceLayer.Dtos;

internal sealed class SapDocumentDto
{
    [JsonPropertyName("DocEntry")]
    public int DocEntry { get; set; }

    [JsonPropertyName("DocNum")]
    public int DocNum { get; set; }

    [JsonPropertyName("CardCode")]
    public string CardCode { get; set; } = string.Empty;

    [JsonPropertyName("CardName")]
    public string? CardName { get; set; }

    [JsonPropertyName("DocDate")]
    public string? DocDate { get; set; }

    [JsonPropertyName("DocDueDate")]
    public string? DocDueDate { get; set; }

    [JsonPropertyName("GroupNum")]
    public int? GroupNum { get; set; }

    [JsonPropertyName("UpdateDate")]
    public string? UpdateDate { get; set; }

    [JsonPropertyName("DocumentLines")]
    public List<SapDocumentLineDto>? DocumentLines { get; set; }
}

internal sealed class SapDocumentLineDto
{
    [JsonPropertyName("TargetType")]
    public int? TargetType { get; set; }

    [JsonPropertyName("TargetEntry")]
    public int? TargetEntry { get; set; }
}
