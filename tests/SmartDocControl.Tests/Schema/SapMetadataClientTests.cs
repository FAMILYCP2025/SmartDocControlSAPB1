using System.Net;
using FluentAssertions;
using SmartDocControl.Schema.Descriptors;
using SmartDocControl.Schema.Sap;
using SmartDocControl.Tests.TestHelpers;
using Xunit;

namespace SmartDocControl.Tests.Schema;

public sealed class SapMetadataClientTests
{
    private static readonly Uri BaseUri = new("https://sap-test:50000/b1s/v1/");

    // ─── GetTableAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetTableAsync_TableExists_ReturnsMetadata()
    {
        var handler = new StubHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "value": [
                    { "TableName": "JCA_DLC_RULE", "TableType": "bott_NoObject" }
                  ]
                }
                """)
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        var result = await client.GetTableAsync("JCA_DLC_RULE");

        result.Should().NotBeNull();
        result!.TableName.Should().Be("JCA_DLC_RULE");
        result.TableType.Should().Be("bott_NoObject");

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.ToString().Should().Contain("UserTablesMD");
        handler.Requests[0].Method.Should().Be(HttpMethod.Get);
    }

    [Fact]
    public async Task GetTableAsync_EmptyValueList_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(req =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "value": [] }""")
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        var result = await client.GetTableAsync("JCA_DLC_MISSING");

        result.Should().BeNull();
    }

    [Fact]
    public async Task GetTableAsync_404_ReturnsNull()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.NotFound));
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        var result = await client.GetTableAsync("JCA_DLC_RULE");

        result.Should().BeNull();
    }

    // ─── GetFieldAsync ────────────────────────────────────────────────────────

    [Fact]
    public async Task GetFieldAsync_FieldExists_ReturnsMetadataWithPrefixedName()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""
                {
                  "value": [
                    { "TableName": "JCA_DLC_RULE", "Name": "U_Active", "Type": "db_Alpha", "EditSize": 1 }
                  ]
                }
                """)
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        var result = await client.GetFieldAsync("JCA_DLC_RULE", "Active");

        result.Should().NotBeNull();
        result!.FieldName.Should().Be("U_Active");
        result.Type.Should().Be("db_Alpha");
        result.Size.Should().Be(1);

        var decodedUri = Uri.UnescapeDataString(handler.Requests[0].RequestUri!.ToString());
        decodedUri.Should().Contain("Name eq 'Active'");
        decodedUri.Should().NotContain("Name eq 'U_Active'");
    }

    [Fact]
    public async Task GetFieldAsync_AcceptsAlreadyPrefixedName_StripsForFilter()
    {
        // Callers may pass the physical CUFD name ("U_Active"); the filter must
        // still use the alias form ("Active") — SAP Service Layer does not accept
        // the "U_" prefix in UserFieldsMD Name filters.
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "value": [] }""")
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        await client.GetFieldAsync("JCA_DLC_RULE", "U_Active");

        var decodedUri = Uri.UnescapeDataString(handler.Requests[0].RequestUri!.ToString());
        decodedUri.Should().Contain("Name eq 'Active'");
        decodedUri.Should().NotContain("Name eq 'U_Active'");
        decodedUri.Should().NotContain("U_U_Active");
    }

    // ─── CreateUserTableAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateUserTableAsync_PostsExpectedBody()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        await client.CreateUserTableAsync(new UdtDescriptor
        {
            Type = "UserTable",
            Operation = "CreateIfNotExists",
            TableName = "JCA_DLC_RULE",
            TableDescription = "Rules",
            TableType = "bott_NoObject"
        });

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].Method.Should().Be(HttpMethod.Post);
        handler.Requests[0].RequestUri!.ToString().Should().EndWith("UserTablesMD");
        capturedBody.Should().Contain("\"TableName\":\"JCA_DLC_RULE\"");
        capturedBody.Should().Contain("\"TableDescription\":\"Rules\"");
        capturedBody.Should().Contain("\"TableType\":\"bott_NoObject\"");
    }

    [Fact]
    public async Task CreateUserTableAsync_AlreadyExists_ThrowsSapObjectAlreadyExistsException()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""
                {
                  "error": {
                    "code": "-2035",
                    "message": { "lang": "en-US", "value": "Object already exists" }
                  }
                }
                """)
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        var act = async () => await client.CreateUserTableAsync(new UdtDescriptor
        {
            TableName = "JCA_DLC_RULE",
            TableDescription = "Rules",
            TableType = "bott_NoObject"
        });

        var ex = (await act.Should().ThrowAsync<SapObjectAlreadyExistsException>()).Which;
        ex.ObjectName.Should().Be("JCA_DLC_RULE");
        ex.ErrorCode.Should().Be("-2035");
    }

    [Fact]
    public async Task CreateUserTableAsync_GenericError_ThrowsSapMetadataException()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.InternalServerError)
            {
                Content = new StringContent("""
                { "error": { "code": "-100", "message": { "value": "boom" } } }
                """)
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        var act = async () => await client.CreateUserTableAsync(new UdtDescriptor
        {
            TableName = "JCA_DLC_RULE",
            TableDescription = "Rules",
            TableType = "bott_NoObject"
        });

        var ex = (await act.Should().ThrowAsync<SapMetadataException>()).Which;
        ex.ErrorCode.Should().Be("-100");
        ex.HttpStatus.Should().Be(500);
    }

    // ─── CreateUserFieldAsync ─────────────────────────────────────────────────

    [Fact]
    public async Task CreateUserFieldAsync_PostsExpectedBody()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        await client.CreateUserFieldAsync(new UdfDescriptor
        {
            TableName = "JCA_DLC_RULE",
            Name = "Active",
            FieldDescription = "Rule active",
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

        handler.Requests.Should().ContainSingle();
        handler.Requests[0].RequestUri!.ToString().Should().EndWith("UserFieldsMD");
        capturedBody.Should().Contain("\"TableName\":\"@JCA_DLC_RULE\"");
        capturedBody.Should().Contain("\"Name\":\"Active\"");
        capturedBody.Should().Contain("\"Type\":\"db_Alpha\"");
        capturedBody.Should().Contain("\"EditSize\":1");
        capturedBody.Should().Contain("\"Mandatory\":\"tYES\"");
        capturedBody.Should().Contain("\"DefaultValue\":\"Y\"");
        capturedBody.Should().Contain("\"ValidValuesMD\"");
        capturedBody.Should().NotContain("\"FieldType\"");
    }

    [Fact]
    public async Task CreateUserFieldAsync_AlreadyExists_ThrowsSapObjectAlreadyExistsException()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.BadRequest)
            {
                Content = new StringContent("""
                { "error": { "code": "-2035", "message": { "value": "exists" } } }
                """)
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        var act = async () => await client.CreateUserFieldAsync(new UdfDescriptor
        {
            TableName = "JCA_DLC_RULE",
            Name = "Active",
            FieldDescription = "x",
            Type = "db_Alpha",
            Size = 1
        });

        var ex = (await act.Should().ThrowAsync<SapObjectAlreadyExistsException>()).Which;
        ex.ObjectName.Should().Be("JCA_DLC_RULE.U_Active");
    }

    // ─── UserFieldsMD TableName normalization (12F-FIX) ───────────────────────

    [Fact]
    public async Task CreateUserFieldAsync_PayloadHasAtPrefixedTableName()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        await client.CreateUserFieldAsync(new UdfDescriptor
        {
            TableName = "JCA_DLC_RULE",
            Name = "Active",
            FieldDescription = "x",
            Type = "db_Alpha",
            Size = 1
        });

        capturedBody.Should().Contain("\"TableName\":\"@JCA_DLC_RULE\"");
        capturedBody.Should().NotContain("\"TableName\":\"JCA_DLC_RULE\"");
    }

    [Fact]
    public async Task CreateUserFieldAsync_DescriptorAlreadyHasAtPrefix_NotDoubled()
    {
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        await client.CreateUserFieldAsync(new UdfDescriptor
        {
            TableName = "@JCA_DLC_RULE",
            Name = "Active",
            FieldDescription = "x",
            Type = "db_Alpha",
            Size = 1
        });

        capturedBody.Should().Contain("\"TableName\":\"@JCA_DLC_RULE\"");
        capturedBody.Should().NotContain("@@JCA_DLC_RULE");
    }

    [Fact]
    public async Task GetFieldAsync_FilterUsesAtPrefixedTableName()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "value": [] }""")
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        await client.GetFieldAsync("JCA_DLC_RULE", "Active");

        // The URI may percent-encode '@' as '%40'; check the decoded form so
        // the assertion is robust to .NET URI normalization.
        var decodedUri = Uri.UnescapeDataString(handler.Requests[0].RequestUri!.ToString());
        decodedUri.Should().Contain("TableName eq '@JCA_DLC_RULE'");
        decodedUri.Should().NotContain("TableName eq 'JCA_DLC_RULE'");
        decodedUri.Should().Contain("Name eq 'Active'");
        decodedUri.Should().NotContain("Name eq 'U_Active'");
    }

    [Fact]
    public async Task GetFieldAsync_WhenCalledWithUPrefixedFieldName_StripsPrefixForServiceLayerFilter()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "value": [] }""")
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        await client.GetFieldAsync("JCA_DLC_RULE", "U_Active");

        var decodedUri = Uri.UnescapeDataString(handler.Requests[0].RequestUri!.ToString());
        decodedUri.Should().Contain("Name eq 'Active'");
        decodedUri.Should().NotContain("U_Active");
    }

    [Fact]
    public async Task GetFieldAsync_WhenCalledWithAliasName_UsesAliasAsIs()
    {
        var handler = new StubHttpMessageHandler(_ =>
            new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent("""{ "value": [] }""")
            });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        await client.GetFieldAsync("JCA_DLC_RULE", "Active");

        var decodedUri = Uri.UnescapeDataString(handler.Requests[0].RequestUri!.ToString());
        decodedUri.Should().Contain("Name eq 'Active'");
        decodedUri.Should().NotContain("U_Active");
    }

    [Fact]
    public async Task CreateUserTableAsync_PayloadKeepsTableNameWithoutAtPrefix()
    {
        // UserTablesMD must NOT use the "@" prefix — only UserFieldsMD does.
        string? capturedBody = null;
        var handler = new StubHttpMessageHandler(req =>
        {
            capturedBody = req.Content!.ReadAsStringAsync().GetAwaiter().GetResult();
            return new HttpResponseMessage(HttpStatusCode.Created);
        });
        using var http = new HttpClient(handler) { BaseAddress = BaseUri };
        var client = new SapMetadataClient(http);

        await client.CreateUserTableAsync(new UdtDescriptor
        {
            TableName = "JCA_DLC_RULE",
            TableDescription = "Rules",
            TableType = "bott_NoObject"
        });

        capturedBody.Should().Contain("\"TableName\":\"JCA_DLC_RULE\"");
        capturedBody.Should().NotContain("\"TableName\":\"@JCA_DLC_RULE\"");
    }

    [Fact]
    public void Ctor_NullHttpClient_Throws()
    {
        var act = () => new SapMetadataClient(null!);
        act.Should().Throw<ArgumentNullException>();
    }
}
