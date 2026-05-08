using System.Text.Json;
using FluentAssertions;
using SmartDocControl.Runner;
using Xunit;

namespace SmartDocControl.Tests.Runner;

public sealed class ConfigurationLoaderTests : IDisposable
{
    private readonly string _tempDir;
    private readonly List<string> _envVarsToCleanup = new();

    public ConfigurationLoaderTests()
    {
        _tempDir = Path.Combine(Path.GetTempPath(), $"sdc_cfg_test_{Guid.NewGuid():N}");
        Directory.CreateDirectory(_tempDir);
    }

    public void Dispose()
    {
        foreach (var name in _envVarsToCleanup)
            Environment.SetEnvironmentVariable(name, null);
        if (Directory.Exists(_tempDir))
            Directory.Delete(_tempDir, true);
    }

    private void WriteJson(string fileName, object content) =>
        File.WriteAllText(Path.Combine(_tempDir, fileName),
            JsonSerializer.Serialize(content, new JsonSerializerOptions { WriteIndented = true }));

    private void SetEnvVar(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value);
        _envVarsToCleanup.Add(name);
    }

    [Fact]
    public void Load_BaseFileOnly_ReturnsSapOptions()
    {
        WriteJson("appsettings.json", new
        {
            Sap = new { BaseUrl = "https://sap-test:50000/b1s/v1/", CompanyDb = "TESTDB", Username = "svc", PasswordEnvironmentVariable = "PWD_VAR" },
            Execution = new { DefaultSimulation = true, MaxRetries = 2, RetryDelaySeconds = 1 },
            Logging = new { LogPath = @"C:\Logs", PendingFunctionalLogPath = @"C:\Logs\Pending" }
        });

        var result = ConfigurationLoader.Load("TST", _tempDir);

        result.Sap.BaseUrl.Should().Be("https://sap-test:50000/b1s/v1/");
        result.Sap.CompanyDb.Should().Be("TESTDB");
        result.Execution.MaxRetries.Should().Be(2);
    }

    [Fact]
    public void Load_WithEnvironmentOverlay_AppliesOverrides()
    {
        WriteJson("appsettings.json", new
        {
            Sap = new { BaseUrl = "https://sap-base:50000/b1s/v1/", CompanyDb = "BASE_DB", Username = "svc", PasswordEnvironmentVariable = "PWD" },
            Execution = new { DefaultSimulation = true, MaxRetries = 3 },
            Logging = new { LogPath = @"C:\Logs", PendingFunctionalLogPath = @"C:\Logs\Pending" }
        });
        WriteJson("appsettings.TST.json", new
        {
            Sap = new { BaseUrl = "https://sap-tst:50000/b1s/v1/", CompanyDb = "TST_DB" },
            Execution = new { MaxRetries = 1 }
        });

        var result = ConfigurationLoader.Load("TST", _tempDir);

        result.Sap.BaseUrl.Should().Be("https://sap-tst:50000/b1s/v1/");
        result.Sap.CompanyDb.Should().Be("TST_DB");
        result.Execution.MaxRetries.Should().Be(1);
        result.Sap.Username.Should().Be("svc"); // unchanged from base
    }

    [Fact]
    public void Load_MissingOverlayFile_DoesNotThrow()
    {
        WriteJson("appsettings.json", new
        {
            Sap = new { BaseUrl = "https://sap-test:50000/b1s/v1/", CompanyDb = "DB", Username = "svc", PasswordEnvironmentVariable = "P" },
            Execution = new { },
            Logging = new { LogPath = @"C:\L", PendingFunctionalLogPath = @"C:\LP" }
        });

        var act = () => ConfigurationLoader.Load("NOPE", _tempDir);

        act.Should().NotThrow();
    }

    [Fact]
    public void Load_ForcesEnvironmentFromCliParameter()
    {
        WriteJson("appsettings.json", new
        {
            Sap = new { BaseUrl = "https://sap-test:50000/b1s/v1/", CompanyDb = "DB", Username = "svc", PasswordEnvironmentVariable = "P" },
            Execution = new { Environment = "WRONG" },
            Logging = new { LogPath = @"C:\L", PendingFunctionalLogPath = @"C:\LP" }
        });

        var result = ConfigurationLoader.Load("TST", _tempDir);

        result.Execution.Environment.Should().Be("TST");
    }

    [Fact]
    public void Load_MissingBaseFile_Throws()
    {
        var act = () => ConfigurationLoader.Load("TST", _tempDir);

        act.Should().Throw<Exception>();
    }
}
