using System.Net;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Sap.Dtos;

namespace SmartDocControl.Schema.Sap;

internal sealed class SapMetadataClient : ISapMetadataProvider, ISchemaExecutor
{
    private const string AlreadyExistsErrorCode = "-2035";

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true,
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private readonly HttpClient _httpClient;

    public SapMetadataClient(HttpClient httpClient)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        _httpClient = httpClient;
    }

    // ─── Read (ISapMetadataProvider) ──────────────────────────────────────────

    public async Task<SapTableMetadata?> GetTableAsync(string tableName)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);

        var path = $"UserTablesMD?$filter=TableName eq '{Escape(tableName)}'";
        using var response = await _httpClient.GetAsync(path).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessOrThrowAsync(response, tableName).ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var parsed = JsonSerializer.Deserialize<SapTableListResponse>(body, JsonOptions);
        var entry = parsed?.Value?.FirstOrDefault();
        if (entry is null) return null;

        return new SapTableMetadata
        {
            TableName = entry.TableName,
            TableType = entry.TableType
        };
    }

    public async Task<SapFieldMetadata?> GetFieldAsync(string tableName, string fieldName)
    {
        ArgumentException.ThrowIfNullOrEmpty(tableName);
        ArgumentException.ThrowIfNullOrEmpty(fieldName);

        var prefixed = fieldName.StartsWith("U_", StringComparison.OrdinalIgnoreCase)
            ? fieldName
            : $"U_{fieldName}";

        var path = $"UserFieldsMD?$filter=TableName eq '{Escape(NormalizeUserFieldTableName(tableName))}' and Name eq '{Escape(prefixed)}'";
        using var response = await _httpClient.GetAsync(path).ConfigureAwait(false);
        if (response.StatusCode == HttpStatusCode.NotFound)
            return null;

        await EnsureSuccessOrThrowAsync(response, $"{tableName}.{prefixed}").ConfigureAwait(false);

        var body = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        var parsed = JsonSerializer.Deserialize<SapFieldListResponse>(body, JsonOptions);
        var entry = parsed?.Value?.FirstOrDefault();
        if (entry is null) return null;

        return new SapFieldMetadata
        {
            TableName = entry.TableName,
            FieldName = entry.Name,
            Type = entry.Type,
            Size = entry.EditSize
        };
    }

    // ─── Write (ISchemaExecutor) ──────────────────────────────────────────────

    public async Task CreateUserTableAsync(UdtDescriptor udt, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(udt);

        var payload = new SapUserTablePayload
        {
            TableName = udt.TableName,
            TableDescription = udt.TableDescription,
            TableType = udt.TableType
        };

        await PostMetadataAsync("UserTablesMD", payload, udt.TableName, cancellationToken).ConfigureAwait(false);
    }

    public async Task CreateUserFieldAsync(UdfDescriptor udf, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(udf);

        var payload = new SapUserFieldPayload
        {
            TableName = NormalizeUserFieldTableName(udf.TableName),
            Name = udf.Name, // SAP adds the "U_" prefix on its side
            Description = udf.FieldDescription,
            Type = udf.Type,
            EditSize = udf.Size,
            Mandatory = udf.Mandatory == true ? "tYES" : null,
            DefaultValue = udf.DefaultValue
        };

        if (udf.ValidValues is { Count: > 0 })
        {
            payload.ValidValuesMD = udf.ValidValues
                .Select(v => new SapValidValuePayload
                {
                    Value = v.Value,
                    Description = v.Description
                })
                .ToList();
        }

        var objectName = $"{udf.TableName}.U_{udf.Name}";
        await PostMetadataAsync("UserFieldsMD", payload, objectName, cancellationToken).ConfigureAwait(false);
    }

    // ─── Internals ────────────────────────────────────────────────────────────

    private async Task PostMetadataAsync<TPayload>(
        string path,
        TPayload payload,
        string objectName,
        CancellationToken cancellationToken)
    {
        var json = JsonSerializer.Serialize(payload, JsonOptions);
        using var content = new StringContent(json, Encoding.UTF8, "application/json");
        using var response = await _httpClient.PostAsync(path, content, cancellationToken).ConfigureAwait(false);

        if (response.IsSuccessStatusCode)
            return;

        var (errorCode, errorMessage) = await TryParseErrorAsync(response).ConfigureAwait(false);

        if (string.Equals(errorCode, AlreadyExistsErrorCode, StringComparison.Ordinal))
            throw new SapObjectAlreadyExistsException(objectName, errorCode,
                $"SAP object '{objectName}' already exists.");

        throw new SapMetadataException(objectName, (int)response.StatusCode, errorCode,
            $"SAP metadata operation failed for '{objectName}' (HTTP {(int)response.StatusCode}): {errorMessage}");
    }

    private static async Task EnsureSuccessOrThrowAsync(HttpResponseMessage response, string objectName)
    {
        if (response.IsSuccessStatusCode) return;

        var (errorCode, errorMessage) = await TryParseErrorAsync(response).ConfigureAwait(false);
        throw new SapMetadataException(objectName, (int)response.StatusCode, errorCode,
            $"SAP metadata query failed for '{objectName}' (HTTP {(int)response.StatusCode}): {errorMessage}");
    }

    private static async Task<(string? code, string message)> TryParseErrorAsync(HttpResponseMessage response)
    {
        var raw = await response.Content.ReadAsStringAsync().ConfigureAwait(false);
        if (string.IsNullOrWhiteSpace(raw))
            return (null, response.ReasonPhrase ?? "(no error body)");

        try
        {
            var envelope = JsonSerializer.Deserialize<SapErrorEnvelope>(raw, JsonOptions);
            var code = envelope?.Error?.Code?.ToString();
            var msg = envelope?.Error?.Message?.Value ?? response.ReasonPhrase ?? raw;
            return (code, msg);
        }
        catch (JsonException)
        {
            return (null, raw);
        }
    }

    private static string Escape(string value) => value.Replace("'", "''");

    /// <summary>
    /// UserFieldsMD identifies the parent UDT with the "@" prefix in both the
    /// POST payload and the OData filter (e.g. "@JCA_DLC_RULE"), even though
    /// UserTablesMD itself uses the bare name. This helper is idempotent: a
    /// table name that already starts with "@" is returned unchanged.
    /// </summary>
    private static string NormalizeUserFieldTableName(string tableName) =>
        tableName.StartsWith('@') ? tableName : $"@{tableName}";
}
