namespace SmartDocControl.Schema.Install;

public sealed record ApplyOptions
{
    public bool DryRun { get; init; }
    public bool ContinueOnError { get; init; }
    public bool TreatAlreadyExistsAsSuccess { get; init; } = true;
    public Action<string>? OnEvent { get; init; }
}
