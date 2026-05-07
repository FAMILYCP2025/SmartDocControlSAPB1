using System.Net;
using System.Text;
using FluentAssertions;
using SmartDocControl.Application.Models;
using SmartDocControl.Infrastructure.Configuration;
using SmartDocControl.Infrastructure.ServiceLayer;
using SmartDocControl.Infrastructure.Validation;
using SmartDocControl.Tests.TestHelpers;
using Xunit;

namespace SmartDocControl.Tests.Infrastructure;

public sealed class StartupValidatorTests : IDisposable
{
    private readonly List<string> _envVarsToCleanup = new();
    private readonly List<string> _dirsToCleanup = new();

    public void Dispose()
    {
        foreach (var name in _envVarsToCleanup)
            Environment.SetEnvironmentVariable(name, null);
        foreach (var dir in _dirsToCleanup)
        {
            try { if (Directory.Exists(dir)) Directory.Delete(dir, true); }
            catch { /* best-effort cleanup */ }
        }
    }

    private string CreateEnvVar(string value)
    {
        var name = $"TEST_PWD_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(name, value);
        _envVarsToCleanup.Add(name);
        return name;
    }

    private string CreateTempDir()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sdc_test_{Guid.NewGuid():N}");
        _dirsToCleanup.Add(dir);
        return dir;
    }

    private static SapOptions ValidSapOptions(string passwordEnvVar) => new()
    {
        BaseUrl = "https://sap-test:50000/b1s/v1/",
        CompanyDb = "TESTDB",
        Username = "svc_test",
        PasswordEnvironmentVariable = passwordEnvVar,
        IgnoreSslErrors = false,
        TimeoutSeconds = 30
    };

    private static ExecutionOptions ValidExecutionOptions(string env = "TST", bool defaultSimulation = true) => new()
    {
        Environment = env,
        DefaultSimulation = defaultSimulation,
        MaxRetries = 3,
        RetryDelaySeconds = 0
    };

    private LoggingOptions ValidLoggingOptions() => new()
    {
        LogPath = CreateTempDir(),
        PendingFunctionalLogPath = CreateTempDir()
    };

    private static StartupValidator CreateValidator(
        SapOptions sap, ExecutionOptions exec, LoggingOptions log,
        Func<HttpRequestMessage, HttpResponseMessage> responder)
    {
        var handler = new StubHttpMessageHandler(responder);
        var http = new HttpClient(handler) { BaseAddress = new Uri(sap.BaseUrl) };
        var slClient = new ServiceLayerClient(http, sap, exec);
        return new StartupValidator(sap, exec, log, slClient);
    }

    private static HttpResponseMessage LoginOk()
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{\"SessionId\":\"abc\"}", Encoding.UTF8, "application/json")
        };
        resp.Headers.Add("Set-Cookie", "B1SESSION=test-session-id; HttpOnly");
        resp.Headers.Add("Set-Cookie", "ROUTEID=.node1; path=/b1s");
        return resp;
    }

    private static HttpResponseMessage LogoutOk() => new(HttpStatusCode.NoContent);

    private static HttpResponseMessage UserTablesOk(params string[] tableNames)
    {
        var json = "{\"value\":[" +
                   string.Join(",", tableNames.Select(t => $"{{\"TableName\":\"{t}\"}}")) +
                   "]}";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private static HttpResponseMessage Status(HttpStatusCode code) => new(code)
    {
        Content = new StringContent("{}", Encoding.UTF8, "application/json")
    };

    private static Func<HttpRequestMessage, HttpResponseMessage> Router(
        Func<HttpRequestMessage, HttpResponseMessage>? loginResp = null,
        Func<HttpRequestMessage, HttpResponseMessage>? logoutResp = null,
        Func<HttpRequestMessage, HttpResponseMessage>? userTablesResp = null)
    {
        return req =>
        {
            var path = req.RequestUri!.AbsolutePath;
            if (path.EndsWith("/Login", StringComparison.OrdinalIgnoreCase))
                return (loginResp ?? (_ => LoginOk()))(req);
            if (path.EndsWith("/Logout", StringComparison.OrdinalIgnoreCase))
                return (logoutResp ?? (_ => LogoutOk()))(req);
            if (path.EndsWith("/UserTablesMD", StringComparison.OrdinalIgnoreCase))
                return (userTablesResp ?? (_ => UserTablesOk("JCA_DLC_RULE", "JCA_DLC_EXC", "JCA_DLC_LOG", "JCA_DLC_RUN")))(req);
            return Status(HttpStatusCode.NotFound);
        };
    }

    [Fact]
    public async Task ValidateAsync_AllValid_ReturnsIsValidTrue()
    {
        var pwd = CreateEnvVar("test-password");
        var sap = ValidSapOptions(pwd);
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.IsValid.Should().BeTrue();
        report.Errors.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_PrdWithIgnoreSsl_ReturnsErrorD05()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        sap.IgnoreSslErrors = true;
        var exec = ValidExecutionOptions(env: "PRD");
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.IsValid.Should().BeFalse();
        report.Errors.Should().Contain(i => i.Code == "SEC-001");
    }

    [Fact]
    public async Task ValidateAsync_TstWithIgnoreSsl_NoSecurityError()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        sap.IgnoreSslErrors = true;
        var exec = ValidExecutionOptions(env: "TST");
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.Errors.Should().NotContain(i => i.Code == "SEC-001");
    }

    [Fact]
    public async Task ValidateAsync_HttpBaseUrl_ReturnsErrorSap002()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        sap.BaseUrl = "http://insecure:50000/b1s/v1/";
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.IsValid.Should().BeFalse();
        report.Errors.Should().Contain(i => i.Code == "SAP-002");
    }

    [Fact]
    public async Task ValidateAsync_EmptyPasswordEnvironmentVariable_ReturnsErrorSap005()
    {
        var sap = ValidSapOptions(string.Empty);
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.IsValid.Should().BeFalse();
        report.Errors.Should().Contain(i => i.Code == "SAP-005");
    }

    [Fact]
    public async Task ValidateAsync_PasswordEnvVarNotSet_ReturnsErrorSap006()
    {
        var nonExistentVar = $"NEVER_SET_{Guid.NewGuid():N}";
        var sap = ValidSapOptions(nonExistentVar);
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.IsValid.Should().BeFalse();
        report.Errors.Should().Contain(i => i.Code == "SAP-006");
    }

    [Fact]
    public async Task ValidateAsync_EmptyCompanyDb_ReturnsErrorSap003()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        sap.CompanyDb = string.Empty;
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.Errors.Should().Contain(i => i.Code == "SAP-003");
    }

    [Fact]
    public async Task ValidateAsync_EmptyUsername_ReturnsErrorSap004()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        sap.Username = string.Empty;
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.Errors.Should().Contain(i => i.Code == "SAP-004");
    }

    [Fact]
    public async Task ValidateAsync_MissingOneUserTable_ReturnsErrorUdt001()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router(
            userTablesResp: _ => UserTablesOk("JCA_DLC_RULE", "JCA_DLC_EXC", "JCA_DLC_LOG")));

        var report = await validator.ValidateAsync();

        report.IsValid.Should().BeFalse();
        report.Errors.Should().ContainSingle(i => i.Code == "UDT-001" && i.Message.Contains("JCA_DLC_RUN"));
    }

    [Fact]
    public async Task ValidateAsync_MissingAllUserTables_ReturnsFourErrors()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router(
            userTablesResp: _ => UserTablesOk()));

        var report = await validator.ValidateAsync();

        report.IsValid.Should().BeFalse();
        report.Errors.Where(i => i.Code == "UDT-001").Should().HaveCount(4);
    }

    [Fact]
    public async Task ValidateAsync_PrdAndDefaultSimulationFalse_AddsWarningSim001()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        var exec = ValidExecutionOptions(env: "PRD", defaultSimulation: false);
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.Warnings.Should().Contain(i => i.Code == "SIM-001");
    }

    [Fact]
    public async Task ValidateAsync_LoginFails_ReturnsErrorSapConn001()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router(
            loginResp: _ => Status(HttpStatusCode.Unauthorized)));

        var report = await validator.ValidateAsync();

        report.IsValid.Should().BeFalse();
        report.Errors.Should().Contain(i => i.Code == "SAP-CONN-001");
    }

    [Fact]
    public async Task ValidateAsync_UserTablesQueryFails_ReturnsErrorUdtConn001()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router(
            userTablesResp: _ => Status(HttpStatusCode.InternalServerError)));

        var report = await validator.ValidateAsync();

        report.IsValid.Should().BeFalse();
        report.Errors.Should().Contain(i => i.Code == "UDT-CONN-001");
    }

    [Fact]
    public async Task ValidateAsync_PayUdtNotValidated_NoErrorEvenIfMissing()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.Errors.Should().NotContain(i => i.Message.Contains("PAY"));
    }

    [Fact]
    public async Task ValidateAsync_EmptyLogPath_ReturnsErrorLog001()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        var exec = ValidExecutionOptions();
        var log = new LoggingOptions
        {
            LogPath = string.Empty,
            PendingFunctionalLogPath = CreateTempDir()
        };

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.Errors.Should().Contain(i => i.Code == "LOG-001");
    }

    [Fact]
    public async Task ValidateAsync_EmptyPendingPath_ReturnsErrorLog002()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        var exec = ValidExecutionOptions();
        var log = new LoggingOptions
        {
            LogPath = CreateTempDir(),
            PendingFunctionalLogPath = string.Empty
        };

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.Errors.Should().Contain(i => i.Code == "LOG-002");
    }

    [Fact]
    public async Task ValidateAsync_LogoutFailure_AddsWarningOnly()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router(
            logoutResp: _ => throw new HttpRequestException("network down")));

        var report = await validator.ValidateAsync();

        report.IsValid.Should().BeTrue();
        report.Warnings.Should().Contain(i => i.Code == "LOGOUT-001");
    }

    [Fact]
    public async Task ValidateAsync_ConfigErrors_SkipsNetworkCalls()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        sap.CompanyDb = string.Empty;
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var handler = new StubHttpMessageHandler(req => Status(HttpStatusCode.OK));
        var http = new HttpClient(handler) { BaseAddress = new Uri(sap.BaseUrl) };
        var slClient = new ServiceLayerClient(http, sap, exec);
        var validator = new StartupValidator(sap, exec, log, slClient);

        var report = await validator.ValidateAsync();

        report.IsValid.Should().BeFalse();
        handler.Requests.Should().BeEmpty();
    }

    [Fact]
    public async Task ValidateAsync_EnvironmentUnknown_AddsWarningExe002()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        var exec = ValidExecutionOptions(env: "STAGE");
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router());

        var report = await validator.ValidateAsync();

        report.Warnings.Should().Contain(i => i.Code == "EXE-002");
    }

    [Fact]
    public async Task ValidateAsync_UserTablesCaseInsensitive_AcceptsMixedCase()
    {
        var pwd = CreateEnvVar("p");
        var sap = ValidSapOptions(pwd);
        var exec = ValidExecutionOptions();
        var log = ValidLoggingOptions();

        var validator = CreateValidator(sap, exec, log, Router(
            userTablesResp: _ => UserTablesOk("jca_dlc_rule", "JCA_DLC_EXC", "Jca_Dlc_Log", "JCA_DLC_RUN")));

        var report = await validator.ValidateAsync();

        report.IsValid.Should().BeTrue();
    }
}
