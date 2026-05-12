using System.Net;
using System.Text.Json;
using FluentAssertions;
using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Sap;
using SmartDocControl.Tests.TestHelpers;
using Xunit;

namespace SmartDocControl.Tests.Schema;

/// <summary>
/// Strict structural assertions on the JSON payload sent to SAP B1 Service Layer
/// metadata endpoints. Uses JsonDocument to verify property presence/absence rather
/// than substring matching, to catch payload-shape regressions before real SAP apply.
/// </summary>
public sealed class SapMetadataClientPayloadShapeTests
{
    private static readonly Uri BaseUri = new("https://sap-test:50000/b1s/v1/");

    // ─── UserTable payload shape ──────────────────────────────────────────────

    [Fact]
    public async Task UserTablePayload_ContainsExactlyExpectedProperties()
    {
        var doc = await CaptureTablePayloadAsync(new UdtDescriptor
        {
            TableName = "JCA_DLC_RULE",
            TableDescription = "Rules",
            TableType = "noObject"
        });

        var props = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        props.Should().BeEquivalentTo("TableName", "TableDescription", "TableType");
        doc.RootElement.GetProperty("TableName").GetString().Should().Be("JCA_DLC_RULE");
        doc.RootElement.GetProperty("TableDescription").GetString().Should().Be("Rules");
        doc.RootElement.GetProperty("TableType").GetString().Should().Be("noObject");
    }

    // ─── UserField — minimal db_Alpha ─────────────────────────────────────────

    [Fact]
    public async Task UserFieldPayload_MinimalDbAlpha_ContainsOnlyRequiredProperties()
    {
        var doc = await CaptureFieldPayloadAsync(new UdfDescriptor
        {
            TableName = "JCA_DLC_RULE",
            Name = "Active",
            FieldDescription = "Active flag",
            Type = "db_Alpha",
            Size = 1
        });

        var props = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        props.Should().BeEquivalentTo("TableName", "Name", "Description", "Type", "EditSize");
        props.Should().NotContain("Mandatory");
        props.Should().NotContain("DefaultValue");
        props.Should().NotContain("ValidValuesMD");
        props.Should().NotContain("FieldType"); // regression: payload must use "Type"
    }

    // ─── UserField — minimal db_Numeric ───────────────────────────────────────

    [Fact]
    public async Task UserFieldPayload_MinimalDbNumeric_ContainsOnlyRequiredProperties()
    {
        var doc = await CaptureFieldPayloadAsync(new UdfDescriptor
        {
            TableName = "JCA_DLC_RULE",
            Name = "GraceDays",
            FieldDescription = "Grace days",
            Type = "db_Numeric",
            Size = 6
        });

        var props = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        props.Should().BeEquivalentTo("TableName", "Name", "Description", "Type", "EditSize");
        doc.RootElement.GetProperty("Type").GetString().Should().Be("db_Numeric");
        doc.RootElement.GetProperty("EditSize").GetInt32().Should().Be(6);
    }

    // ─── UserField — full db_Alpha ────────────────────────────────────────────

    [Fact]
    public async Task UserFieldPayload_FullDbAlpha_ContainsAllProperties()
    {
        var doc = await CaptureFieldPayloadAsync(new UdfDescriptor
        {
            TableName = "JCA_DLC_RULE",
            Name = "Active",
            FieldDescription = "Active flag",
            Type = "db_Alpha",
            Size = 1,
            DefaultValue = "Y",
            Mandatory = true,
            ValidValues =
            [
                new ValidValueDescriptor { Value = "Y", Description = "Yes" },
                new ValidValueDescriptor { Value = "N", Description = "No" }
            ]
        });

        var props = doc.RootElement.EnumerateObject().Select(p => p.Name).ToArray();

        props.Should().BeEquivalentTo(
            "TableName", "Name", "Description", "Type",
            "EditSize", "Mandatory", "DefaultValue", "ValidValuesMD");

        doc.RootElement.GetProperty("Mandatory").GetString().Should().Be("tYES");
        doc.RootElement.GetProperty("DefaultValue").GetString().Should().Be("Y");
        var validValues = doc.RootElement.GetProperty("ValidValuesMD");
        validValues.GetArrayLength().Should().Be(2);
        validValues[0].GetProperty("Value").GetString().Should().Be("Y");
        validValues[0].GetProperty("Description").GetString().Should().Be("Yes");
        validValues[1].GetProperty("Value").GetString().Should().Be("N");
    }

    // ─── UserField — Mandatory variations ─────────────────────────────────────

    [Fact]
    public async Task UserFieldPayload_MandatoryTrue_EmitsTYES()
    {
        var doc = await CaptureFieldPayloadAsync(BuildMinimalAlpha() with { Mandatory = true });

        doc.RootElement.TryGetProperty("Mandatory", out var mandatory).Should().BeTrue();
        mandatory.GetString().Should().Be("tYES");
    }

    [Fact]
    public async Task UserFieldPayload_MandatoryFalse_OmitsMandatoryEntirely()
    {
        var doc = await CaptureFieldPayloadAsync(BuildMinimalAlpha() with { Mandatory = false });

        doc.RootElement.TryGetProperty("Mandatory", out _).Should().BeFalse();
    }

    [Fact]
    public async Task UserFieldPayload_MandatoryNull_OmitsMandatoryEntirely()
    {
        var doc = await CaptureFieldPayloadAsync(BuildMinimalAlpha()); // Mandatory not set

        doc.RootElement.TryGetProperty("Mandatory", out _).Should().BeFalse();
    }

    // ─── UserField — DefaultValue variations ──────────────────────────────────

    [Fact]
    public async Task UserFieldPayload_NoDefaultValue_OmitsDefaultValueEntirely()
    {
        var doc = await CaptureFieldPayloadAsync(BuildMinimalAlpha());

        doc.RootElement.TryGetProperty("DefaultValue", out _).Should().BeFalse();
    }

    [Fact]
    public async Task UserFieldPayload_WithDefaultValue_EmitsDefaultValue()
    {
        var doc = await CaptureFieldPayloadAsync(BuildMinimalAlpha() with { DefaultValue = "Y" });

        doc.RootElement.GetProperty("DefaultValue").GetString().Should().Be("Y");
    }

    // ─── UserField — ValidValues variations ───────────────────────────────────

    [Fact]
    public async Task UserFieldPayload_NoValidValues_OmitsValidValuesMDEntirely()
    {
        var doc = await CaptureFieldPayloadAsync(BuildMinimalAlpha());

        doc.RootElement.TryGetProperty("ValidValuesMD", out _).Should().BeFalse();
    }

    [Fact]
    public async Task UserFieldPayload_EmptyValidValuesList_OmitsValidValuesMDEntirely()
    {
        var doc = await CaptureFieldPayloadAsync(BuildMinimalAlpha() with
        {
            ValidValues = Array.Empty<ValidValueDescriptor>()
        });

        doc.RootElement.TryGetProperty("ValidValuesMD", out _).Should().BeFalse();
    }

    [Fact]
    public async Task UserFieldPayload_NameDoesNotIncludeUPrefix()
    {
        var doc = await CaptureFieldPayloadAsync(BuildMinimalAlpha());

        doc.RootElement.GetProperty("Name").GetString().Should().Be("Active");
        doc.RootElement.GetProperty("Name").GetString().Should().NotStartWith("U_");
    }

    // ─── Regression: Type property name (C2) ──────────────────────────────────

    [Fact]
    public async Task UserFieldPayload_UsesTypePropertyNotFieldType()
    {
        var doc = await CaptureFieldPayloadAsync(BuildMinimalAlpha());

        doc.RootElement.TryGetProperty("Type", out var typeProp).Should().BeTrue(
            "SAP B1 Service Layer UserFieldsMD payload uses 'Type', not 'FieldType'.");
        typeProp.GetString().Should().Be("db_Alpha");

        doc.RootElement.TryGetProperty("FieldType", out _).Should().BeFalse(
            "Legacy 'FieldType' property name must not appear in the payload.");
    }

    // ─── Helpers ──────────────────────────────────────────────────────────────

    private static UdfDescriptor BuildMinimalAlpha() => new()
    {
        TableName = "JCA_DLC_RULE",
        Name = "Active",
        FieldDescription = "Active flag",
        Type = "db_Alpha",
        Size = 1
    };

    private static async Task<JsonDocument> CaptureTablePayloadAsync(UdtDescriptor udt)
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        await client.CreateUserTableAsync(udt);

        capturedBody.Should().NotBeNull();
        return JsonDocument.Parse(capturedBody!);
    }

    private static async Task<JsonDocument> CaptureFieldPayloadAsync(UdfDescriptor udf)
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        await client.CreateUserFieldAsync(udf);

        capturedBody.Should().NotBeNull();
        return JsonDocument.Parse(capturedBody!);
    }
}
