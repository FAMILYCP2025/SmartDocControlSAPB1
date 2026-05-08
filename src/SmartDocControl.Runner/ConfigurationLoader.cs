using Microsoft.Extensions.Configuration;
using SmartDocControl.Infrastructure.Configuration;

namespace SmartDocControl.Runner;

internal static class ConfigurationLoader
{
    public static AppConfiguration Load(string environment, string? basePath = null)
    {
        basePath ??= AppContext.BaseDirectory;

        var config = new ConfigurationBuilder()
            .SetBasePath(basePath)
            .AddJsonFile("appsettings.json", optional: false, reloadOnChange: false)
            .AddJsonFile($"appsettings.{environment}.json", optional: true, reloadOnChange: false)
            .AddEnvironmentVariables()
            .Build();

        var sap = config.GetSection("Sap").Get<SapOptions>() ?? new SapOptions();
        var exec = config.GetSection("Execution").Get<ExecutionOptions>() ?? new ExecutionOptions();
        var log = config.GetSection("Logging").Get<LoggingOptions>() ?? new LoggingOptions();

        // CLI --environment always wins over any JSON/env-var setting
        exec.Environment = environment;

        return new AppConfiguration(sap, exec, log);
    }
}
