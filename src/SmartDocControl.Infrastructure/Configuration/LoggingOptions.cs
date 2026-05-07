namespace SmartDocControl.Infrastructure.Configuration;

public sealed class LoggingOptions
{
    public string LogPath { get; set; } = @"C:\SmartDocControl\Logs\";
    public string PendingFunctionalLogPath { get; set; } = @"C:\SmartDocControl\PendingLogs\";
    public bool DebugMode { get; set; }
    public int FileSizeLimitMb { get; set; } = 50;
    public int RetainedFileCountLimit { get; set; } = 30;
}
