using System.Net;
using SmartDocControl.Application.Exceptions;
using SmartDocControl.Infrastructure.Configuration;
using SmartDocControl.Infrastructure.Logging;
using SmartDocControl.Infrastructure.ServiceLayer;
using SmartDocControl.Schema.Install;
using SmartDocControl.Schema.Loader;
using SmartDocControl.Schema.Sap;

namespace SmartDocControl.Runner.Commands;

/// <summary>
/// Orchestrator for --install-schema. In this release only --dry-run is supported:
/// it logs in to SAP, runs INSPECT + PLAN against real Service Layer metadata
/// endpoints (GETs only), prints the resulting InstallPlan, and exits.
/// Apply is NOT invoked. No POST is sent to UserTablesMD or UserFieldsMD.
/// </summary>
internal static class InstallSchemaCommand
{
    private const string InstallPasswordEnvVar = "SAP_INSTALL_PASSWORD";
    private const string SchemaSubfolder = "schema/v1";

    public static async Task<int> RunAsync(
        AppConfiguration config,
        string runId,
        FileLogger? logger,
        CliOptions opts,
        CancellationToken cancellationToken = default)
    {
        // Guard 1: in this release, real APPLY is not implemented. --install-schema requires --dry-run.
        if (!opts.DryRun)
        {
            Console.Error.WriteLine(
                "[ERROR] --install-schema requires --dry-run in this release. Real APPLY is not yet implemented.");
            logger?.Warning("Install-schema invoked without --dry-run; aborting.");
            return ExitCodes.UsageError;
        }

        // Guard 2: install credential must be set in a separate env var from the runtime credential.
        var installPassword = Environment.GetEnvironmentVariable(InstallPasswordEnvVar);
        if (string.IsNullOrWhiteSpace(installPassword))
        {
            Console.Error.WriteLine(
                $"[ERROR] Environment variable '{InstallPasswordEnvVar}' is not set or is empty.");
            Console.Error.WriteLine(
                "        The schema installer requires a separate elevated-permissions credential.");
            logger?.Error($"{InstallPasswordEnvVar} not set; aborting install-schema.");
            return ExitCodes.FatalConfig;
        }

        // Guard 3: descriptors folder must exist (copied next to the runner binary).
        var schemaDir = Path.Combine(AppContext.BaseDirectory, SchemaSubfolder);
        if (!Directory.Exists(schemaDir))
        {
            Console.Error.WriteLine($"[ERROR] Schema descriptors folder not found: '{schemaDir}'");
            logger?.Error($"Schema descriptors folder not found: {schemaDir}");
            return ExitCodes.FatalConfig;
        }

        Console.WriteLine($"Running schema installer (dry-run) — descriptors: {schemaDir}");
        Console.WriteLine();

        // Load + validate descriptors before touching SAP.
        LoadedSchema loaded;
        try
        {
            var loader = new DescriptorLoader();
            loaded = loader.Load(schemaDir);

            var validator = new DescriptorValidator();
            validator.Validate(loaded.Manifest);
            foreach (var udt in loaded.UserTables) validator.Validate(udt);
            foreach (var udf in loaded.UserFields) validator.Validate(udf);
        }
        catch (DescriptorValidationException ex)
        {
            Console.Error.WriteLine($"[ERROR] Descriptor validation failed: {ex.Message}");
            logger?.Error("Descriptor validation failed.", ex);
            return ExitCodes.FatalConfig;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[ERROR] Failed to load schema descriptors: {ex.Message}");
            logger?.Error("Failed to load schema descriptors.", ex);
            return ExitCodes.FatalConfig;
        }

        Console.WriteLine($"Schema version : {loaded.Manifest.SchemaVersion}");
        Console.WriteLine($"Descriptors    : {loaded.UserTables.Count} UDT(s), {loaded.UserFields.Count} UDF(s)");
        Console.WriteLine();

        // Build HttpClient with CookieContainer so the session captured during Login
        // is available to subsequent GETs from SapMetadataClient.
        var cookieContainer = new CookieContainer();
        var httpHandler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = cookieContainer
        };
        if (config.Sap.IgnoreSslErrors)
            httpHandler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        using var httpClient = new HttpClient(httpHandler)
        {
            BaseAddress = new Uri(config.Sap.BaseUrl),
            Timeout = TimeSpan.FromSeconds(config.Sap.TimeoutSeconds)
        };

        // Clone SapOptions to point the installer login at SAP_INSTALL_PASSWORD
        // instead of the runtime SAP_AUTOCLOSE_PASSWORD.
        var installSapOptions = new SapOptions
        {
            BaseUrl = config.Sap.BaseUrl,
            CompanyDb = config.Sap.CompanyDb,
            Username = config.Sap.Username,
            PasswordEnvironmentVariable = InstallPasswordEnvVar,
            IgnoreSslErrors = config.Sap.IgnoreSslErrors,
            TimeoutSeconds = config.Sap.TimeoutSeconds
        };

        var slClient = new ServiceLayerClient(httpClient, installSapOptions, config.Execution)
        {
            CorrelationId = runId
        };

        // Defensive: pass only the READ interface to the installer. Write methods
        // (ISchemaExecutor) are unreachable through this variable.
        ISapMetadataProvider metadataProvider = new SapMetadataClient(httpClient);
        var installer = new SchemaInstaller();

        InstallPlan plan;
        try
        {
            Console.WriteLine("Logging in to SAP Service Layer...");
            await slClient.LoginAsync(cancellationToken);
            logger?.Information("Installer logged in to SAP Service Layer.");

            Console.WriteLine("Inspecting SAP metadata and building install plan...");
            Console.WriteLine();
            plan = await installer.PlanAsync(loaded, metadataProvider);
            logger?.Information(
                $"Install plan built: creates={plan.TotalCreates}, skips={plan.TotalSkips}, drifts={plan.TotalDrifts}.");
        }
        catch (SapAuthenticationException ex)
        {
            Console.Error.WriteLine($"[ERROR] SAP authentication failed: {ex.Message}");
            logger?.Error("SAP authentication failed during install-schema.", ex);
            return ExitCodes.ValidationFailed;
        }
        catch (SapMetadataException ex)
        {
            Console.Error.WriteLine($"[ERROR] SAP metadata query failed: {ex.Message}");
            logger?.Error("SAP metadata query failed during install-schema.", ex);
            return ExitCodes.SchemaInstallFailed;
        }
        catch (Exception ex)
        {
            Console.Error.WriteLine($"[FATAL] Unexpected error during install-schema: {ex.Message}");
            logger?.Error("Unexpected error during install-schema.", ex);
            return ExitCodes.UnhandledFatal;
        }
        finally
        {
            // Best-effort logout. Failure here is non-fatal.
            try { await slClient.LogoutAsync(cancellationToken); }
            catch (Exception ex) { logger?.Warning($"Logout failed: {ex.Message}"); }
        }

        ConsoleOutputFormatter.PrintInstallPlan(plan, dryRun: true);

        if (plan.HasBlockingIssues)
            return ExitCodes.SchemaDriftDetected;

        return ExitCodes.Success;
    }
}
