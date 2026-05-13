using SmartDocControl.Application.Models;
using SmartDocControl.Schema.Install;

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
        Console.WriteLine("Usage: SmartDocControl.Runner --environment <ENV> [mode-flags]");
        Console.WriteLine();
        Console.WriteLine("Arguments:");
        Console.WriteLine("  --environment, -e <ENV>  Required. Target environment (DEV, TST, PRD).");
        Console.WriteLine("                           Loads appsettings.{ENV}.json overlay if present.");
        Console.WriteLine();
        Console.WriteLine("Modes (choose one):");
        Console.WriteLine("  --validate-only          Validate configuration and SAP connectivity, then exit.");
        Console.WriteLine("  --install-schema         Run the schema installer. Combine with --dry-run or --force.");
        Console.WriteLine();
        Console.WriteLine("Modifiers:");
        Console.WriteLine("  --dry-run                With --install-schema: read-only INSPECT + PLAN, no SAP writes.");
        Console.WriteLine("                           Standalone: alias for --validate-only (backward compat).");
        Console.WriteLine("  --force                  With --install-schema (without --dry-run): authorize real apply");
        Console.WriteLine("                           against SAP Service Layer (POST UserTablesMD / UserFieldsMD).");
        Console.WriteLine("  --trace-metadata         Log each SAP Service Layer HTTP request (method, URI, body).");
        Console.WriteLine("                           Sensitive headers (Cookie, Authorization) are omitted.");
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

    public static void PrintInstallPlan(InstallPlan plan, bool dryRun)
    {
        var saved = Console.ForegroundColor;

        Console.WriteLine("INSTALL PLAN");
        Console.WriteLine("============");

        if (plan.Entries.Count == 0)
        {
            Console.WriteLine("  (empty — no entries to apply)");
        }

        foreach (var entry in plan.Entries)
        {
            var (tag, color) = entry.Action switch
            {
                InstallAction.Create => ("[CREATE]", ConsoleColor.Green),
                InstallAction.Skip   => ("[SKIP]  ", ConsoleColor.DarkGray),
                InstallAction.Drift  => ("[DRIFT] ", ConsoleColor.Red),
                _                    => ("[?]     ", saved)
            };

            Console.ForegroundColor = color;
            var typeLabel = entry.ObjectType == InstallObjectType.UserTable ? "UserTable" : "UserField";
            var blockingMark = entry.IsBlocking ? "  (BLOCKING)" : "";
            Console.WriteLine($"  {tag} {typeLabel,-10}  {entry.ObjectName}{blockingMark}");
            Console.ForegroundColor = saved;
            Console.WriteLine($"             {entry.Reason}");
        }

        Console.WriteLine();
        Console.WriteLine("SUMMARY");
        Console.WriteLine("=======");
        Console.WriteLine($"  Total entries  : {plan.Entries.Count}");
        Console.WriteLine($"  Creates        : {plan.TotalCreates}");
        Console.WriteLine($"  Skips          : {plan.TotalSkips}");
        Console.WriteLine($"  Drifts         : {plan.TotalDrifts}");
        Console.WriteLine($"  Blocking       : {(plan.HasBlockingIssues ? "YES" : "No")}");
        Console.WriteLine();

        if (plan.HasBlockingIssues)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[ABORT] Plan has blocking drift(s). Run cannot proceed.");
            Console.ForegroundColor = saved;
        }
        else if (dryRun)
        {
            Console.ForegroundColor = ConsoleColor.Yellow;
            Console.WriteLine("[DRY-RUN] No changes applied to SAP.");
            Console.ForegroundColor = saved;
        }
    }

    public static void PrintApplyResult(SchemaApplyResult result, TimeSpan elapsed)
    {
        var saved = Console.ForegroundColor;

        Console.WriteLine();
        Console.WriteLine("APPLY RESULT");
        Console.WriteLine("============");

        if (result.Entries.Count == 0)
            Console.WriteLine("  (no entries)");

        foreach (var entry in result.Entries)
        {
            var (tag, color) = entry.Status switch
            {
                SchemaApplyStatus.Created       => ("[CREATED]       ", ConsoleColor.Green),
                SchemaApplyStatus.AlreadyExists => ("[ALREADY-EXISTS]", ConsoleColor.DarkYellow),
                SchemaApplyStatus.Skipped       => ("[SKIPPED]       ", ConsoleColor.DarkGray),
                SchemaApplyStatus.DryRun        => ("[DRY-RUN]       ", ConsoleColor.Yellow),
                SchemaApplyStatus.Failed        => ("[FAILED]        ", ConsoleColor.Red),
                SchemaApplyStatus.Aborted       => ("[ABORTED]       ", ConsoleColor.Red),
                _                               => ("[?]             ", saved)
            };

            Console.ForegroundColor = color;
            var typeLabel = entry.ObjectType == InstallObjectType.UserTable ? "UserTable" : "UserField";
            Console.WriteLine($"  {tag} {typeLabel,-10}  {entry.ObjectName}");
            Console.ForegroundColor = saved;

            if (!string.IsNullOrEmpty(entry.Message))
                Console.WriteLine($"                   {entry.Message}");
        }

        Console.WriteLine();
        Console.WriteLine("SUMMARY");
        Console.WriteLine("=======");
        Console.WriteLine($"  Created         : {result.TotalCreated}");
        Console.WriteLine($"  Already existed : {result.TotalAlreadyExisted}");
        Console.WriteLine($"  Skipped         : {result.TotalSkipped}");
        Console.WriteLine($"  Failed          : {result.TotalFailed}");
        Console.WriteLine($"  Aborted         : {result.TotalAborted}");
        Console.WriteLine($"  Elapsed         : {elapsed.TotalSeconds:F2}s");
        Console.WriteLine();

        if (result.IsSuccessful)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine("[OK] Apply finished successfully.");
        }
        else if (result.WasAborted)
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine($"[ABORT] {result.AbortReason ?? "Apply was aborted."}");
        }
        else
        {
            Console.ForegroundColor = ConsoleColor.Red;
            Console.WriteLine("[FAIL] Apply completed with failures.");
        }
        Console.ForegroundColor = saved;
    }

    public static void PrintPostValidationReport(PostValidationReport report)
    {
        var saved = Console.ForegroundColor;

        if (report.IsValid)
        {
            Console.ForegroundColor = ConsoleColor.Green;
            Console.WriteLine($"  [OK] All {report.VerifiedCount} object(s) verified present in SAP.");
            Console.ForegroundColor = saved;
            return;
        }

        Console.ForegroundColor = ConsoleColor.Red;
        Console.WriteLine($"  [FAIL] Verified {report.VerifiedCount} object(s); {report.Missing.Count} missing:");
        foreach (var item in report.Missing)
        {
            var typeLabel = item.ObjectType == InstallObjectType.UserTable ? "UserTable" : "UserField";
            Console.WriteLine($"    - {typeLabel}: {item.ObjectName}");
            if (!string.IsNullOrEmpty(item.Reason))
            {
                Console.ForegroundColor = saved;
                Console.WriteLine($"        {item.Reason}");
                Console.ForegroundColor = ConsoleColor.Red;
            }
        }
        Console.ForegroundColor = saved;
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
