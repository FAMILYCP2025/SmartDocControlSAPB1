using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;

namespace SmartDocControl.Schema.Tracking;

/// <summary>
/// Writes a registry row to the @JCA_DLC_SCHEMA UDT after a successful schema
/// install. Each successful run creates a new row; no upsert in this version.
/// </summary>
public sealed class SchemaVersionRepository
{
    private const string EntitySet = "@JCA_DLC_SCHEMA";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;

    public SchemaVersionRepository(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    public async Task RegisterAsync(SchemaVersionEntry entry, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(entry);

        var payload = BuildPayload(entry);
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(EntitySet, content, cancellationToken)
            .ConfigureAwait(false);

        if (!response.IsSuccessStatusCode)
        {
            var body = await response.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            throw new InvalidOperationException(
                $"Failed to register schema version (HTTP {(int)response.StatusCode}): {body}");
        }
    }

    /// <summary>
    /// The SAP built-in "Code" field on bott_NoObject UDTs is limited to
    /// 20 characters. Format kept compact: {version}-{yyMMddHHmmss}
    /// (e.g. "1.0.0-260513021530" = 18 chars).
    /// </summary>
    internal static SchemaVersionPayload BuildPayload(SchemaVersionEntry entry)
    {
        var utc = entry.AppliedAtUtc.UtcDateTime;
        var compactTimestamp = utc.ToString("yyMMddHHmmss");

        return new SchemaVersionPayload
        {
            Code = $"{entry.SchemaVersion}-{compactTimestamp}",
            Name = $"Schema {entry.SchemaVersion}",
            SchemaVersion = entry.SchemaVersion,
            AppVersion = entry.AppVersion,
            Environment = entry.Environment,
            AppliedAtUtc = utc.ToString("yyyy-MM-dd'T'HH:mm:ss'Z'"),
            RequiredObjects = entry.RequiredObjects,
            VerifiedObjects = entry.VerifiedObjects,
            Status = entry.Status,
            RunId = entry.RunId
        };
    }

    internal sealed class SchemaVersionPayload
    {
        [JsonPropertyName("Code")] public string Code { get; set; } = string.Empty;
        [JsonPropertyName("Name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("U_SchemaVersion")] public string SchemaVersion { get; set; } = string.Empty;
        [JsonPropertyName("U_AppVersion")] public string? AppVersion { get; set; }
        [JsonPropertyName("U_Environment")] public string Environment { get; set; } = string.Empty;
        [JsonPropertyName("U_AppliedAtUtc")] public string AppliedAtUtc { get; set; } = string.Empty;
        [JsonPropertyName("U_RequiredObjects")] public int RequiredObjects { get; set; }
        [JsonPropertyName("U_VerifiedObjects")] public int VerifiedObjects { get; set; }
        [JsonPropertyName("U_Status")] public string Status { get; set; } = string.Empty;
        [JsonPropertyName("U_RunId")] public string? RunId { get; set; }
    }
}
