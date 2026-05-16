namespace SmartDocControl.Schema.Tracking;

/// <summary>
/// Domain record describing the outcome of one successful schema install run.
/// Persisted as a row in the @JCA_DLC_SCHEMA UDT by <see cref="SchemaVersionRepository"/>.
/// </summary>
public sealed class SchemaVersionEntry
{
    public string SchemaVersion { get; init; } = string.Empty;
    public string? AppVersion { get; init; }
    public string Environment { get; init; } = string.Empty;
    public DateTimeOffset AppliedAtUtc { get; init; }
    public int RequiredObjects { get; init; }
    public int VerifiedObjects { get; init; }
    public string Status { get; init; } = string.Empty;
    public string? RunId { get; init; }
}
