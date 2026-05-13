using SmartDocControl.Schema.Sap;

namespace SmartDocControl.Schema.Install;

public sealed record ApplyOptions
{
    public bool DryRun { get; init; }
    public bool ContinueOnError { get; init; }
    public bool TreatAlreadyExistsAsSuccess { get; init; } = true;
    public Action<string>? OnEvent { get; init; }
    /// <summary>How long to wait between metadata-availability polls after a UDT is created.</summary>
    public TimeSpan MetadataPropagationPollInterval { get; init; } = TimeSpan.FromSeconds(1);
    /// <summary>Maximum total time to wait for a freshly created UDT to become visible via GET.</summary>
    public TimeSpan MetadataPropagationTimeout { get; init; } = TimeSpan.FromSeconds(30);
    /// <summary>
    /// When set, a logout+login is performed once before the first UDF POST so that SAP
    /// rebuilds its session metadata cache and can see UDTs that were created moments ago.
    /// </summary>
    public ISapMetadataSessionRefresher? SessionRefresher { get; init; }
}
