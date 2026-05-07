using SmartDocControl.Application.Models;
using SmartDocControl.Application.Ports;
using SmartDocControl.Infrastructure.Configuration;
using SmartDocControl.Infrastructure.ServiceLayer;

namespace SmartDocControl.Infrastructure.Validation;

public sealed class StartupValidator : IStartupValidator
{
    private const string PrdEnvironment = "PRD";

    private static readonly string[] RequiredUserTables =
    {
        "JCA_DLC_RULE",
        "JCA_DLC_EXC",
        "JCA_DLC_LOG",
        "JCA_DLC_RUN"
    };

    private static readonly string[] KnownEnvironments = { "DEV", "TST", "PRD" };

    private readonly SapOptions _sapOptions;
    private readonly ExecutionOptions _executionOptions;
    private readonly LoggingOptions _loggingOptions;
    private readonly ServiceLayerClient _serviceLayerClient;

    public StartupValidator(
        SapOptions sapOptions,
        ExecutionOptions executionOptions,
        LoggingOptions loggingOptions,
        ServiceLayerClient serviceLayerClient)
    {
        ArgumentNullException.ThrowIfNull(sapOptions);
        ArgumentNullException.ThrowIfNull(executionOptions);
        ArgumentNullException.ThrowIfNull(loggingOptions);
        ArgumentNullException.ThrowIfNull(serviceLayerClient);

        _sapOptions = sapOptions;
        _executionOptions = executionOptions;
        _loggingOptions = loggingOptions;
        _serviceLayerClient = serviceLayerClient;
    }

    public async Task<StartupValidationReport> ValidateAsync(CancellationToken cancellationToken = default)
    {
        var issues = new List<ValidationIssue>();

        ValidateSapOptions(issues);
        ValidateExecutionOptions(issues);
        ValidateSecurityRules(issues);
        ValidatePasswordEnvironmentVariable(issues);
        ValidateLoggingPaths(issues);

        if (issues.Any(i => i.Severity == ValidationSeverity.Error))
            return new StartupValidationReport(issues);

        await ValidateSapConnectivityAsync(issues, cancellationToken);

        return new StartupValidationReport(issues);
    }

    private void ValidateSapOptions(List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(_sapOptions.BaseUrl))
        {
            issues.Add(new ValidationIssue("SAP-001", ValidationSeverity.Error,
                "SapOptions.BaseUrl is not configured."));
        }
        else if (!_sapOptions.BaseUrl.StartsWith("https://", StringComparison.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue("SAP-002", ValidationSeverity.Error,
                $"SapOptions.BaseUrl must use HTTPS scheme (got: '{_sapOptions.BaseUrl}')."));
        }

        if (string.IsNullOrWhiteSpace(_sapOptions.CompanyDb))
            issues.Add(new ValidationIssue("SAP-003", ValidationSeverity.Error,
                "SapOptions.CompanyDb is not configured."));

        if (string.IsNullOrWhiteSpace(_sapOptions.Username))
            issues.Add(new ValidationIssue("SAP-004", ValidationSeverity.Error,
                "SapOptions.Username is not configured."));
    }

    private void ValidateExecutionOptions(List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(_executionOptions.Environment))
        {
            issues.Add(new ValidationIssue("EXE-001", ValidationSeverity.Error,
                "ExecutionOptions.Environment is not configured."));
            return;
        }

        if (!KnownEnvironments.Contains(_executionOptions.Environment, StringComparer.OrdinalIgnoreCase))
        {
            issues.Add(new ValidationIssue("EXE-002", ValidationSeverity.Warning,
                $"ExecutionOptions.Environment value '{_executionOptions.Environment}' is not one of DEV/TST/PRD."));
        }
    }

    private void ValidateSecurityRules(List<ValidationIssue> issues)
    {
        var isPrd = string.Equals(_executionOptions.Environment, PrdEnvironment, StringComparison.OrdinalIgnoreCase);

        if (isPrd && _sapOptions.IgnoreSslErrors)
        {
            issues.Add(new ValidationIssue("SEC-001", ValidationSeverity.Error,
                "D05: IgnoreSslErrors=true is forbidden in PRD environment."));
        }

        if (isPrd && !_executionOptions.DefaultSimulation)
        {
            issues.Add(new ValidationIssue("SIM-001", ValidationSeverity.Warning,
                "D10: PRD environment with DefaultSimulation=false. Real document closure may occur without explicit confirmation."));
        }
    }

    private void ValidatePasswordEnvironmentVariable(List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(_sapOptions.PasswordEnvironmentVariable))
        {
            issues.Add(new ValidationIssue("SAP-005", ValidationSeverity.Error,
                "SapOptions.PasswordEnvironmentVariable is not configured."));
            return;
        }

        var password = Environment.GetEnvironmentVariable(_sapOptions.PasswordEnvironmentVariable);
        if (string.IsNullOrEmpty(password))
        {
            issues.Add(new ValidationIssue("SAP-006", ValidationSeverity.Error,
                $"Password environment variable '{_sapOptions.PasswordEnvironmentVariable}' is not set or is empty."));
        }
    }

    private void ValidateLoggingPaths(List<ValidationIssue> issues)
    {
        if (string.IsNullOrWhiteSpace(_loggingOptions.LogPath))
        {
            issues.Add(new ValidationIssue("LOG-001", ValidationSeverity.Error,
                "LoggingOptions.LogPath is not configured."));
        }
        else if (!CanCreateAndWriteToDirectory(_loggingOptions.LogPath, out var error))
        {
            issues.Add(new ValidationIssue("LOG-003", ValidationSeverity.Error,
                $"LogPath '{_loggingOptions.LogPath}' is not writable: {error}"));
        }

        if (string.IsNullOrWhiteSpace(_loggingOptions.PendingFunctionalLogPath))
        {
            issues.Add(new ValidationIssue("LOG-002", ValidationSeverity.Error,
                "LoggingOptions.PendingFunctionalLogPath is not configured."));
        }
        else if (!CanCreateAndWriteToDirectory(_loggingOptions.PendingFunctionalLogPath, out var error))
        {
            issues.Add(new ValidationIssue("LOG-004", ValidationSeverity.Error,
                $"PendingFunctionalLogPath '{_loggingOptions.PendingFunctionalLogPath}' is not writable: {error}"));
        }
    }

    private static bool CanCreateAndWriteToDirectory(string path, out string error)
    {
        error = string.Empty;
        try
        {
            Directory.CreateDirectory(path);
            var probeFile = Path.Combine(path, $".write_probe_{Guid.NewGuid():N}.tmp");
            File.WriteAllText(probeFile, "probe");
            File.Delete(probeFile);
            return true;
        }
        catch (Exception ex)
        {
            error = ex.Message;
            return false;
        }
    }

    private async Task ValidateSapConnectivityAsync(
        List<ValidationIssue> issues,
        CancellationToken cancellationToken)
    {
        try
        {
            await _serviceLayerClient.LoginAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            issues.Add(new ValidationIssue("SAP-CONN-001", ValidationSeverity.Error,
                $"SAP Service Layer login failed: {ex.Message}"));
            return;
        }

        try
        {
            var existing = await _serviceLayerClient.GetExistingUserTablesAsync(
                RequiredUserTables, cancellationToken);

            foreach (var required in RequiredUserTables)
            {
                if (!existing.Contains(required))
                {
                    issues.Add(new ValidationIssue("UDT-001", ValidationSeverity.Error,
                        $"D06: Required UDT '@{required}' not found in SAP."));
                }
            }
        }
        catch (Exception ex)
        {
            issues.Add(new ValidationIssue("UDT-CONN-001", ValidationSeverity.Error,
                $"Failed to query SAP user tables: {ex.Message}"));
        }
        finally
        {
            try
            {
                await _serviceLayerClient.LogoutAsync(cancellationToken);
            }
            catch (Exception ex)
            {
                issues.Add(new ValidationIssue("LOGOUT-001", ValidationSeverity.Warning,
                    $"SAP logout during validation failed (non-blocking): {ex.Message}"));
            }
        }
    }
}
