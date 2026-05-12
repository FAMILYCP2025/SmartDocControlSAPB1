namespace SmartDocControl.Schema.Install;

public enum SchemaApplyStatus
{
    Created,
    AlreadyExists,
    Skipped,
    DryRun,
    Failed,
    Aborted
}
