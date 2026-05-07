using System.Net;
using System.Text;
using FluentAssertions;
using SmartDocControl.Infrastructure.Configuration;
using SmartDocControl.Infrastructure.ServiceLayer;
using SmartDocControl.Tests.TestHelpers;
using Xunit;

namespace SmartDocControl.Tests.Infrastructure;

public sealed class ServiceLayerClientTests
{
    private static SapOptions DefaultSapOptions(string pwdEnvVar = "TEST_PWD") => new()
    {
        BaseUrl = "https://sap-test:50000/b1s/v1/",
        CompanyDb = "TESTDB",
        Username = "svc",
        PasswordEnvironmentVariable = pwdEnvVar
    };

    [Fact]
    public void Constructor_NullBaseAddress_ThrowsArgumentException()
    {
        var http = new HttpClient();
        var act = () => new ServiceLayerClient(http, DefaultSapOptions());

        act.Should().Throw<ArgumentException>().WithMessage("*BaseAddress*");
    }

    [Fact]
    public void Constructor_BaseAddressWithoutTrailingSlash_NormalizesAutomatically()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://sap-test:50000/b1s/v1") };

        _ = new ServiceLayerClient(http, DefaultSapOptions());

        http.BaseAddress!.AbsoluteUri.Should().Be("https://sap-test:50000/b1s/v1/");
    }

    [Fact]
    public void Constructor_BaseAddressWithTrailingSlash_LeavesUnchanged()
    {
        var http = new HttpClient { BaseAddress = new Uri("https://sap-test:50000/b1s/v1/") };

        _ = new ServiceLayerClient(http, DefaultSapOptions());

        http.BaseAddress!.AbsoluteUri.Should().Be("https://sap-test:50000/b1s/v1/");
    }

    [Fact]
    public async Task GetExistingUserTablesAsync_NoActiveSession_Throws()
    {
        var handler = new StubHttpMessageHandler(_ => new HttpResponseMessage(HttpStatusCode.OK));
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://sap-test:50000/b1s/v1/") };
        var client = new ServiceLayerClient(http, DefaultSapOptions());

        var act = async () => await client.GetExistingUserTablesAsync(new[] { "JCA_DLC_RULE" });

        await act.Should().ThrowAsync<InvalidOperationException>().WithMessage("*No active SAP session*");
    }

    [Fact]
    public async Task GetExistingUserTablesAsync_ReturnsCaseInsensitiveSet()
    {
        var pwdVar = $"PWD_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(pwdVar, "p");
        try
        {
            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith("/Login"))
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}", Encoding.UTF8, "application/json")
                    };
                    resp.Headers.Add("Set-Cookie", "B1SESSION=abc; HttpOnly");
                    return resp;
                }
                if (req.RequestUri!.AbsolutePath.EndsWith("/UserTablesMD"))
                {
                    var json = "{\"value\":[{\"TableName\":\"JCA_DLC_RULE\"},{\"TableName\":\"JCA_DLC_EXC\"}]}";
                    return new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent(json, Encoding.UTF8, "application/json")
                    };
                }
                return new HttpResponseMessage(HttpStatusCode.NotFound);
            });
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://sap-test:50000/b1s/v1/") };
            var client = new ServiceLayerClient(http, DefaultSapOptions(pwdVar));

            await client.LoginAsync();
            var result = await client.GetExistingUserTablesAsync(new[] { "JCA_DLC_RULE", "JCA_DLC_EXC" });

            result.Should().Contain("jca_dlc_rule");
            result.Should().Contain("JCA_DLC_RULE");
            result.Should().Contain("Jca_Dlc_Exc");
        }
        finally
        {
            Environment.SetEnvironmentVariable(pwdVar, null);
        }
    }

    [Fact]
    public async Task GetExistingUserTablesAsync_EmptyTableNames_ReturnsEmptySetWithoutNetworkCall()
    {
        var pwdVar = $"PWD_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(pwdVar, "p");
        try
        {
            var handler = new StubHttpMessageHandler(req =>
            {
                if (req.RequestUri!.AbsolutePath.EndsWith("/Login"))
                {
                    var resp = new HttpResponseMessage(HttpStatusCode.OK)
                    {
                        Content = new StringContent("{}", Encoding.UTF8, "application/json")
                    };
                    resp.Headers.Add("Set-Cookie", "B1SESSION=abc");
                    return resp;
                }
                return new HttpResponseMessage(HttpStatusCode.OK)
                {
                    Content = new StringContent("{\"value\":[]}", Encoding.UTF8, "application/json")
                };
            });
            var http = new HttpClient(handler) { BaseAddress = new Uri("https://sap-test:50000/b1s/v1/") };
            var client = new ServiceLayerClient(http, DefaultSapOptions(pwdVar));

            await client.LoginAsync();
            var requestsBeforeQuery = handler.Requests.Count;
            var result = await client.GetExistingUserTablesAsync(Array.Empty<string>());

            result.Should().BeEmpty();
            handler.Requests.Count.Should().Be(requestsBeforeQuery, "no HTTP call should be made for an empty input");
        }
        finally
        {
            Environment.SetEnvironmentVariable(pwdVar, null);
        }
    }
}
