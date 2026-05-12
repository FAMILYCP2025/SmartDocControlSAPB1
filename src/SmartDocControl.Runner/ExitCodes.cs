namespace SmartDocControl.Runner;

internal static class ExitCodes
{
    public const int Success = 0;
    public const int UsageError = 1;
    public const int ValidationFailed = 2;
    public const int FatalConfig = 3;
    public const int UnhandledFatal = 4;
    public const int SchemaInstallFailed = 5;
    public const int SchemaDriftDetected = 6;
}
