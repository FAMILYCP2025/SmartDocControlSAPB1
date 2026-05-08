using SmartDocControl.Infrastructure.Logging;
using SmartDocControl.Infrastructure.ServiceLayer;
using SmartDocControl.Infrastructure.Validation;

namespace SmartDocControl.Runner;

internal static class Program
{
    internal static async Task<int> Main(string[] args)
    {
        var parseResult = CliParseResult.Parse(args);

        if (parseResult.Options?.ShowHelp == true)
        {
            ConsoleOutputFormatter.PrintUsage();
            return ExitCodes.Success;
        }

        if (!parseResult.IsSuccess)
        {
            Console.Error.WriteLine($"Error: {parseResult.Error}");
            ConsoleOutputFormatter.PrintUsage();
            return ExitCodes.UsageError;
        }

        var opts = parseResult.Options!;

        AppConfiguration appConfig;
        try
        {
            appConfig = ConfigurationLoader.Load(opts.Environment);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL] Failed to load configuration: {ex.Message}");
            return ExitCodes.FatalConfig;
        }

        var runId = Guid.NewGuid().ToString("N")[..8].ToUpperInvariant();

        FileLogger? logger = null;
        try
        {
            logger = new FileLogger(appConfig.Logging, runId);
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[WARNING] RUNNING WITHOUT TECHNICAL FILE LOGGER: {ex.Message}");
        }

        var baseUrlDisplay = ConsoleOutputFormatter.GetBaseUrlDisplay(appConfig.Sap.BaseUrl);
        ConsoleOutputFormatter.PrintBanner(runId, opts.Environment, baseUrlDisplay);
        logger?.Information($"Runner started. Environment={opts.Environment}, ValidateOnly={opts.ValidateOnly}");

        if (opts.ValidateOnly)
            return await RunValidateOnlyAsync(appConfig, runId, logger);

        Console.WriteLine("Document processing not yet implemented in this release.");
        logger?.Information("Exiting: document processing not yet implemented.");
        return ExitCodes.Success;
    }

    private static async Task<int> RunValidateOnlyAsync(
        AppConfiguration config, string runId, FileLogger? logger)
    {
        Console.WriteLine("Running startup validation...");
        Console.WriteLine();

        try
        {
            var httpHandler = new HttpClientHandler();
            if (config.Sap.IgnoreSslErrors)
                httpHandler.ServerCertificateCustomValidationCallback =
                    HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

            using var httpClient = new HttpClient(httpHandler)
            {
                BaseAddress = new Uri(config.Sap.BaseUrl),
                Timeout = TimeSpan.FromSeconds(config.Sap.TimeoutSeconds)
            };

            var slClient = new ServiceLayerClient(httpClient, config.Sap, config.Execution)
            {
                CorrelationId = runId
            };

            var validator = new StartupValidator(
                config.Sap, config.Execution, config.Logging, slClient);

            var report = await validator.ValidateAsync();

            ConsoleOutputFormatter.PrintValidationReport(report);

            if (report.IsValid)
            {
                logger?.Information("Startup validation passed.");
                return ExitCodes.Success;
            }

            logger?.Warning($"Startup validation failed with {report.Errors.Count} error(s).");
            return ExitCodes.ValidationFailed;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL] Unexpected error during validation: {ex.Message}");
            logger?.Error("Unexpected error during validation.", ex);
            return ExitCodes.UnhandledFatal;
        }
    }
}
