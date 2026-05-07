namespace SmartDocControl.Infrastructure.Configuration;

public sealed class ExecutionOptions
{
    public string Environment { get; set; } = string.Empty;
    public bool DefaultSimulation { get; set; } = true;
    public int MaxRetries { get; set; } = 3;
    public int RetryDelaySeconds { get; set; } = 2;
    public int[] RetryableHttpStatusCodes { get; set; } = [408, 429, 500, 502, 503, 504];
    public int TimeoutSeconds { get; set; } = 60;
    public bool PreventParallelRuns { get; set; } = true;
    public int StaleRunThresholdHours { get; set; } = 4;
}
