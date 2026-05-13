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
}
