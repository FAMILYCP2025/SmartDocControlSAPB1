using System.Diagnostics;
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
/// Orchestrator for --install-schema.
/// Two supported modes:
///   --install-schema --dry-run   → INSPECT + PLAN only (no POSTs, no writes).
///   --install-schema --force     → INSPECT + PLAN, then real APPLY against SAP
///                                  Service Layer (POST UserTablesMD / UserFieldsMD),
///                                  followed by post-validation re-query.
/// Plain --install-schema (without --dry-run and without --force) is rejected
/// to avoid accidental real apply. Drift detected during planning aborts before
/// any POST, regardless of --force.
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
        // Mode gate: real apply requires --force; --dry-run is always allowed.
        if (!opts.DryRun && !opts.Force)
        {
            Console.Error.WriteLine(
                "[ERROR] --install-schema without --dry-run is a real apply against SAP and requires --force.");
            Console.Error.WriteLine(
                "        Use '--install-schema --dry-run' to preview, or add '--force' to authorize real apply.");
            logger?.Warning("Install-schema invoked without --dry-run and without --force; aborting.");
            return ExitCodes.UsageError;
        }

        // Guard: install credential must be set in a separate env var from the runtime credential.
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

        // Guard: descriptors folder must exist (copied next to the runner binary).
        var schemaDir = Path.Combine(AppContext.BaseDirectory, SchemaSubfolder);
        if (!Directory.Exists(schemaDir))
        {
            Console.Error.WriteLine($"[ERROR] Schema descriptors folder not found: '{schemaDir}'");
            logger?.Error($"Schema descriptors folder not found: {schemaDir}");
            return ExitCodes.FatalConfig;
        }

        var mode = opts.DryRun ? "dry-run" : "REAL APPLY";
        Console.WriteLine($"Running schema installer ({mode}) — descriptors: {schemaDir}");
        Console.WriteLine();

        // Load + validate descriptors before touching SAP.
        LoadedSchema loaded;
        try
        {
            loaded = LoadAndValidateDescriptors(schemaDir);
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

        // HttpClient with CookieContainer so the session captured during Login
        // is available to subsequent GETs/POSTs through SapMetadataClient.
        var cookieContainer = new CookieContainer();
        var httpHandler = new HttpClientHandler
        {
            UseCookies = true,
            CookieContainer = cookieContainer
        };
        if (config.Sap.IgnoreSslErrors)
            httpHandler.ServerCertificateCustomValidationCallback =
                HttpClientHandler.DangerousAcceptAnyServerCertificateValidator;

        HttpMessageHandler outerHandler = httpHandler;
        if (opts.TraceMetadata)
        {
            var tracer = new MetadataTraceHandler(msg =>
            {
                Console.WriteLine(msg);
                logger?.Information(msg);
            });
            tracer.InnerHandler = httpHandler;
            outerHandler = tracer;
        }

        using var httpClient = new HttpClient(outerHandler)
        {
            BaseAddress = new Uri(config.Sap.BaseUrl),
            Timeout = TimeSpan.FromSeconds(config.Sap.TimeoutSeconds)
        };

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

        // SapMetadataClient implements both ISapMetadataProvider (read) and
        // ISchemaExecutor (write). We keep the read interface for dry-run; only
        // the apply branch unwraps the write interface.
        var sapClient = new SapMetadataClient(httpClient);
        ISapMetadataProvider metadataProvider = sapClient;

        var installer = new SchemaInstaller();

        try
        {
            Console.WriteLine("Logging in to SAP Service Layer...");
            await slClient.LoginAsync(cancellationToken);
            logger?.Information("Installer logged in to SAP Service Layer.");

            Console.WriteLine("Inspecting SAP metadata and building install plan...");
            Console.WriteLine();
            var plan = await installer.PlanAsync(loaded, metadataProvider);
            logger?.Information(
                $"Install plan built: creates={plan.TotalCreates}, skips={plan.TotalSkips}, drifts={plan.TotalDrifts}.");

            ConsoleOutputFormatter.PrintInstallPlan(plan, dryRun: opts.DryRun);

            // Drift blocks the apply unconditionally; --force does NOT override drift.
            if (plan.HasBlockingIssues)
            {
                logger?.Warning("Plan has blocking drift(s); apply aborted before any POST.");
                return ExitCodes.SchemaDriftDetected;
            }

            if (opts.DryRun)
                return ExitCodes.Success;

            return await RunRealApplyAsync(installer, plan, loaded, sapClient, metadataProvider, slClient, logger, cancellationToken);
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
            try { await slClient.LogoutAsync(cancellationToken); }
            catch (Exception ex) { logger?.Warning($"Logout failed: {ex.Message}"); }
        }
    }

    private static async Task<int> RunRealApplyAsync(
        SchemaInstaller installer,
        InstallPlan plan,
        LoadedSchema loaded,
        SapMetadataClient sapClient,
        ISapMetadataProvider metadataProvider,
        ServiceLayerClient slClient,
        FileLogger? logger,
        CancellationToken cancellationToken)
    {
        Console.WriteLine();
        Console.WriteLine("APPLYING SCHEMA CHANGES TO SAP");
        Console.WriteLine("==============================");
        Console.WriteLine();

        ISchemaExecutor executor = sapClient;

        var applyOptions = new ApplyOptions
        {
            DryRun = false,
            TreatAlreadyExistsAsSuccess = true,
            ContinueOnError = false,
            SessionRefresher = new ServiceLayerSessionRefresher(slClient),
            OnEvent = e =>
            {
                Console.WriteLine($"  {e}");
                logger?.Information($"[apply] {e}");
            }
        };

        var stopwatch = Stopwatch.StartNew();
        SchemaApplyResult applyResult;
        try
        {
            applyResult = await installer.ApplyAsync(plan, loaded, executor, applyOptions, cancellationToken);
        }
        finally
        {
            stopwatch.Stop();
        }

        ConsoleOutputFormatter.PrintApplyResult(applyResult, stopwatch.Elapsed);

        if (!applyResult.IsSuccessful)
        {
            logger?.Warning(
                $"Schema apply finished with failures: failed={applyResult.TotalFailed}, aborted={applyResult.TotalAborted}.");
            return ExitCodes.SchemaInstallFailed;
        }

        Console.WriteLine();
        Console.WriteLine("POST-VALIDATION");
        Console.WriteLine("===============");

        var report = await installer.VerifyAppliedAsync(
            applyResult, metadataProvider, cancellationToken: cancellationToken);

        ConsoleOutputFormatter.PrintPostValidationReport(report);

        if (!report.IsValid)
        {
            logger?.Warning(
                $"Post-validation failed: {report.Missing.Count} object(s) missing in SAP after apply.");
            return ExitCodes.SchemaInstallFailed;
        }

        logger?.Information($"Schema apply succeeded. Verified {report.VerifiedCount} object(s).");
        return ExitCodes.Success;
    }

    private static LoadedSchema LoadAndValidateDescriptors(string schemaDir)
    {
        var loader = new DescriptorLoader();
        var loaded = loader.Load(schemaDir);

        var validator = new DescriptorValidator();
        validator.Validate(loaded.Manifest);
        foreach (var udt in loaded.UserTables) validator.Validate(udt);
        foreach (var udf in loaded.UserFields) validator.Validate(udf);
        return loaded;
    }
}
