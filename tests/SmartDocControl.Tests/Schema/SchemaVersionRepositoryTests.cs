using System.Net;
using System.Text.Json;
using FluentAssertions;
using SmartDocControl.Schema.Tracking;
using SmartDocControl.Tests.TestHelpers;
using Xunit;

namespace SmartDocControl.Tests.Schema;

public sealed class SchemaVersionRepositoryTests
{
    private static readonly Uri BaseUri = new("https://sap-test:50000/b1s/v1/");

    [Fact]
    public async Task RegisterAsync_PostsToSchemaUdtEndpoint()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.Created));
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var repo = new SchemaVersionRepository(http);

        await repo.RegisterAsync(BuildEntry());

        handler.Requests.Should().ContainSingle();
        var req = handler.Requests[0];
        req.Method.Should().Be(HttpMethod.Post);
        Uri.UnescapeDataString(req.RequestUri!.ToString()).Should().EndWith("@JCA_DLC_SCHEMA");
    }

    [Fact]
    public async Task RegisterAsync_PayloadIncludesCodeAndName()
    {
        var (body, _) = await CaptureBodyAsync(BuildEntry());

        body.Should().Contain("\"Code\":");
        body.Should().Contain("\"Name\":\"Schema 1.0.0\"");
    }

    [Fact]
    public async Task RegisterAsync_PayloadIncludesAllUdfFields()
    {
        var entry = BuildEntry(
            schemaVersion: "1.0.0",
            appVersion: "0.2.0-alpha",
            environment: "TST",
            required: 16,
            verified: 16,
            status: "Success",
            runId: "abc123");

        var (body, _) = await CaptureBodyAsync(entry);

        body.Should().Contain("\"U_SchemaVersion\":\"1.0.0\"");
        body.Should().Contain("\"U_AppVersion\":\"0.2.0-alpha\"");
        body.Should().Contain("\"U_Environment\":\"TST\"");
        body.Should().Contain("\"U_AppliedAtUtc\":");
        body.Should().Contain("\"U_Status\":\"Success\"");
        body.Should().Contain("\"U_RunId\":\"abc123\"");
    }

    [Fact]
    public async Task RegisterAsync_NumericFieldsSerializedAsJsonNumbers()
    {
        // SAP db_Numeric expects JSON numbers, not strings.
        var (body, _) = await CaptureBodyAsync(BuildEntry(required: 16, verified: 14));

        body.Should().Contain("\"U_RequiredObjects\":16");
        body.Should().Contain("\"U_VerifiedObjects\":14");
        body.Should().NotContain("\"U_RequiredObjects\":\"16\"");
        body.Should().NotContain("\"U_VerifiedObjects\":\"14\"");
    }

    [Fact]
    public async Task RegisterAsync_CodeFitsIn20Chars()
    {
        var entry = BuildEntry(
            schemaVersion: "1.0.0",
            appliedAtUtc: new DateTimeOffset(2026, 5, 13, 2, 15, 30, TimeSpan.Zero));

        var (_, payload) = await CaptureBodyAsync(entry);

        payload.GetProperty("Code").GetString().Should().Be("1.0.0-260513021530");
        payload.GetProperty("Code").GetString()!.Length.Should().BeLessThanOrEqualTo(20);
    }

    [Fact]
    public async Task RegisterAsync_AppliedAtUtcSerializedAsIso8601()
    {
        var entry = BuildEntry(
            appliedAtUtc: new DateTimeOffset(2026, 5, 13, 2, 15, 30, TimeSpan.Zero));

        var (_, payload) = await CaptureBodyAsync(entry);

        payload.GetProperty("U_AppliedAtUtc").GetString().Should().Be("2026-05-13T02:15:30Z");
    }

    [Fact]
    public async Task RegisterAsync_NullAppVersionAndRunId_Omitted()
    {
        var entry = BuildEntry(appVersion: null, runId: null);

        var (body, _) = await CaptureBodyAsync(entry);

        body.Should().NotContain("U_AppVersion");
        body.Should().NotContain("U_RunId");
    }

    [Fact]
    public async Task RegisterAsync_HttpFailure_Throws()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""{ "error": { "code": "-2028", "message": { "value": "denied" } } }""")
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var repo = new SchemaVersionRepository(http);

        var act = async () => await repo.RegisterAsync(BuildEntry());

        await act.Should().ThrowAsync<InvalidOperationException>()
            .WithMessage("*Failed to register schema version*400*");
    }

    [Fact]
    public async Task RegisterAsync_NullEntry_Throws()
    {
        using var http = new HttpClient(new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.Created)))
        {
            BaseAddress = BaseUri
        };
        var repo = new SchemaVersionRepository(http);

        var act = async () => await repo.RegisterAsync(null!);

        await act.Should().ThrowAsync<ArgumentNullException>();
    }

    [Fact]
    public void Ctor_NullHttpClient_Throws()
    {
        var act = () => new SchemaVersionRepository(null!);
        act.Should().Throw<ArgumentNullException>();
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static async Task<(string raw, JsonElement parsed)> CaptureBodyAsync(SchemaVersionEntry entry)
    {
        string? captured = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            captured = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var repo = new SchemaVersionRepository(http);

        await repo.RegisterAsync(entry);

        captured.Should().NotBeNull();
        using var doc = JsonDocument.Parse(captured!);
        return (captured!, doc.RootElement.Clone());
    }

    private static SchemaVersionEntry BuildEntry(
        string schemaVersion = "1.0.0",
        string? appVersion = "0.2.0-alpha",
        string environment = "TST",
        DateTimeOffset? appliedAtUtc = null,
        int required = 16,
        int verified = 16,
        string status = "Success",
        string? runId = "run-123") =>
        new()
        {
            SchemaVersion = schemaVersion,
            AppVersion = appVersion,
            Environment = environment,
            AppliedAtUtc = appliedAtUtc ?? DateTimeOffset.UtcNow,
            RequiredObjects = required,
            VerifiedObjects = verified,
            Status = status,
            RunId = runId
        };
}
