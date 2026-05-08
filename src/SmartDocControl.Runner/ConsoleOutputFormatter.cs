using SmartDocControl.Application.Models;

namespace SmartDocControl.Runner;

internal static class ConsoleOutputFormatter
{
    public static void PrintBanner(string runId, string environment, string baseUrlDisplay)
    {
        Console.WriteLine("Smart Document Control — SAP Business One");
        Console.WriteLine($"  Environment : {environment}");
        Console.WriteLine($"  SAP         : {baseUrlDisplay}");
        Console.WriteLine($"  Run ID      : {runId}");
        Console.WriteLine();
    }

    public static void PrintUsage()
    {
        Console.WriteLine("Usage: SmartDocControl.Runner --environment <ENV> [--validate-only]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  --environment, -e <ENV>  Required. Target environment (DEV, TST, PRD).");
        Console.WriteLine("                           Loads appsettings.{ENV}.json overlay if present.");
        Console.WriteLine("  --validate-only          Validate configuration and SAP connectivity, then exit.");
        Console.WriteLine("  --dry-run                Alias for --validate-only.");
        Console.WriteLine("  --help, -h               Show this help.");
    }

    public static void PrintValidationReport(StartupValidationReport report)
    {
        var savedColor = Console.ForegroundColor;
        try
        {
            if (report.IsValid)
            {
                Console.ForegroundColor = ConsoleColor.Green;
                Console.WriteLine("Validation: OK");
            }
            else
            {
                Console.ForegroundColor = ConsoleColor.Red;
                Console.WriteLine("Validation: FAILED");
            }
        }
        finally
        {
            Console.ForegroundColor = savedColor;
        }

        foreach (var issue in report.Errors)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"  [ERR] {issue.Code}: {issue.Message}");
            Console.ForegroundColor = savedColor;
        }

        foreach (var issue in report.Warnings)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine($"  [WRN] {issue.Code}: {issue.Message}");
            Console.ForegroundColor = savedColor;
        }

        Console.WriteLine($"  Validated at: {report.ValidatedAt:yyyy-MM-dd HH:mm:ss} UTC");
    }

    internal static string GetBaseUrlDisplay(string baseUrl)
    {
        if (!Uri.TryCreate(baseUrl, UriKind.Absolute, out var uri))
            return baseUrl;

        return uri.IsDefaultPort
            ? $"{uri.Scheme}://{uri.Host}"
            : $"{uri.Scheme}://{uri.Host}:{uri.Port}";
    }
}
