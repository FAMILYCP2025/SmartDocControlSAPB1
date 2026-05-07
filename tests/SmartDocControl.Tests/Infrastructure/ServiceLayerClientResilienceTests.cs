using System.Net;
using System.Net.Sockets;
using System.Text;
using FluentAssertions;
using SmartDocControl.Application.Exceptions;
using SmartDocControl.Infrastructure.Configuration;
using SmartDocControl.Infrastructure.ServiceLayer;
using SmartDocControl.Tests.TestHelpers;
using Xunit;

namespace SmartDocControl.Tests.Infrastructure;

public sealed class ServiceLayerClientResilienceTests : IDisposable
{
    private readonly List<string> _envVarsToCleanup = new();

    public void Dispose()
    {
        foreach (var name in _envVarsToCleanup)
            Environment.SetEnvironmentVariable(name, null);
    }

    private string CreateEnvVar(string value = "test-password")
    {
        var name = $"TEST_PWD_{Guid.NewGuid():N}";
        Environment.SetEnvironmentVariable(name, value);
        _envVarsToCleanup.Add(name);
        return name;
    }

    private SapOptions DefaultSap(string envVar) => new()
    {
        BaseUrl = "https://sap-test:50000/b1s/v1/",
        CompanyDb = "TST",
        Username = "u",
        PasswordEnvironmentVariable = envVar
    };

    private static ExecutionOptions FastExec(int maxRetries = 3) => new()
    {
        Environment = "TST",
        MaxRetries = maxRetries,
        RetryDelaySeconds = 0
    };

    private static HttpResponseMessage LoginOk()
    {
        var resp = new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("{}", Encoding.UTF8, "application/json")
        };
        resp.Headers.Add("Set-Cookie", "B1SESSION=session-1; HttpOnly");
        return resp;
    }

    private static HttpResponseMessage Status(HttpStatusCode code, string body = "{}")
        => new(code) { Content = new StringContent(body, Encoding.UTF8, "application/json") };

    private static HttpResponseMessage UserTablesOk(params string[] names)
    {
        var json = "{\"value\":[" + string.Join(",", names.Select(n => $"{{\"TableName\":\"{n}\"}}")) + "]}";
        return new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
    }

    private (ServiceLayerClient client, StubHttpMessageHandler handler) Build(
        string envVar,
        Func<HttpRequestMessage, HttpResponseMessage> responder,
        int maxRetries = 3)
    {
        var handler = new StubHttpMessageHandler(responder);
        var http = new HttpClient(handler) { BaseAddress = new Uri("https://sap-test:50000/b1s/v1/") };
        var client = new ServiceLayerClient(http, DefaultSap(envVar), FastExec(maxRetries));
        return (client, handler);
    }

    private async Task<ServiceLayerClient> LoggedInClient(
        string envVar,
        Func<HttpRequestMessage, HttpResponseMessage> postLoginResponder,
        int maxRetries = 3)
    {
        Func<HttpRequestMessage, HttpResponseMessage> router = req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/Login"))
                return LoginOk();
            return postLoginResponder(req);
        };
        var (client, _) = Build(envVar, router, maxRetries);
        await client.LoginAsync();
        return client;
    }

    // ---------- Retry transient ----------

    [Fact]
    public async Task GetAsync_TransientThenSuccess_ReturnsResult()
    {
        var pwd = CreateEnvVar();
        var calls = 0;
        var client = await LoggedInClient(pwd, req =>
        {
            calls++;
            return calls == 1
                ? Status(HttpStatusCode.ServiceUnavailable)
                : UserTablesOk("X");
        });

        var result = await client.GetExistingUserTablesAsync(new[] { "X" });

        result.Should().Contain("X");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_AlwaysTransient_ThrowsSapTransientExceptionAfterRetries()
    {
        var pwd = CreateEnvVar();
        var calls = 0;
        var client = await LoggedInClient(pwd, req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/UserTablesMD"))
            {
                calls++;
                return Status(HttpStatusCode.ServiceUnavailable);
            }
            return Status(HttpStatusCode.NotFound);
        }, maxRetries: 3);

        var act = async () => await client.GetExistingUserTablesAsync(new[] { "X" });

        var ex = (await act.Should().ThrowAsync<SapTransientException>()).Which;
        ex.AttemptsMade.Should().Be(4);
        ex.StatusCode.Should().Be(HttpStatusCode.ServiceUnavailable);
        calls.Should().Be(4);
    }

    [Fact]
    public async Task GetAsync_400_ThrowsSapFunctionalExceptionWithoutRetry()
    {
        var pwd = CreateEnvVar();
        var calls = 0;
        var client = await LoggedInClient(pwd, req =>
        {
            calls++;
            return Status(HttpStatusCode.BadRequest,
                "{\"error\":{\"code\":\"-1\",\"message\":{\"value\":\"bad query\"}}}");
        });

        var act = async () => await client.GetExistingUserTablesAsync(new[] { "X" });

        var ex = (await act.Should().ThrowAsync<SapFunctionalException>()).Which;
        ex.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        ex.SapErrorCode.Should().Be("-1");
        ex.SapErrorMessage.Should().Be("bad query");
        calls.Should().Be(1);
    }

    [Fact]
    public async Task GetAsync_404_ThrowsSapFunctionalException()
    {
        var pwd = CreateEnvVar();
        var client = await LoggedInClient(pwd, _ => Status(HttpStatusCode.NotFound));

        var act = async () => await client.GetExistingUserTablesAsync(new[] { "X" });

        await act.Should().ThrowAsync<SapFunctionalException>();
    }

    [Fact]
    public async Task GetAsync_408ThenSuccess_RetriesOnce()
    {
        var pwd = CreateEnvVar();
        var calls = 0;
        var client = await LoggedInClient(pwd, _ =>
        {
            calls++;
            return calls == 1 ? Status(HttpStatusCode.RequestTimeout) : UserTablesOk("X");
        });

        var result = await client.GetExistingUserTablesAsync(new[] { "X" });

        result.Should().Contain("X");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_429ThenSuccess_RetriesOnce()
    {
        var pwd = CreateEnvVar();
        var calls = 0;
        var client = await LoggedInClient(pwd, _ =>
        {
            calls++;
            return calls == 1 ? Status((HttpStatusCode)429) : UserTablesOk("X");
        });

        var result = await client.GetExistingUserTablesAsync(new[] { "X" });

        result.Should().Contain("X");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_TimeoutThenSuccess_RetriesOnTaskCanceled()
    {
        var pwd = CreateEnvVar();
        var calls = 0;
        var client = await LoggedInClient(pwd, _ =>
        {
            calls++;
            if (calls == 1)
                throw new TaskCanceledException("HttpClient timeout");
            return UserTablesOk("X");
        });

        var result = await client.GetExistingUserTablesAsync(new[] { "X" });

        result.Should().Contain("X");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_ConnectionResetThenSuccess_RetriesOnSocketException()
    {
        var pwd = CreateEnvVar();
        var calls = 0;
        var client = await LoggedInClient(pwd, _ =>
        {
            calls++;
            if (calls == 1)
                throw new HttpRequestException("reset", new SocketException((int)SocketError.ConnectionReset));
            return UserTablesOk("X");
        });

        var result = await client.GetExistingUserTablesAsync(new[] { "X" });

        result.Should().Contain("X");
        calls.Should().Be(2);
    }

    [Fact]
    public async Task GetAsync_UserCancellation_PropagatesOperationCanceledException()
    {
        var pwd = CreateEnvVar();
        var client = await LoggedInClient(pwd, _ => UserTablesOk("X"));

        using var cts = new CancellationTokenSource();
        cts.Cancel();

        var act = async () => await client.GetExistingUserTablesAsync(new[] { "X" }, cts.Token);

        await act.Should().ThrowAsync<OperationCanceledException>();
    }

    // ---------- 401 re-login ----------

    [Fact]
    public async Task GetAsync_401ThenReLoginSuccess_RetriesAndReturns()
    {
        var pwd = CreateEnvVar();
        var queryCalls = 0;
        var loginCalls = 0;

        var (client, _) = Build(pwd, req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/Login"))
            {
                loginCalls++;
                return LoginOk();
            }
            queryCalls++;
            return queryCalls == 1
                ? Status(HttpStatusCode.Unauthorized)
                : UserTablesOk("X");
        });

        await client.LoginAsync();
        var result = await client.GetExistingUserTablesAsync(new[] { "X" });

        result.Should().Contain("X");
        loginCalls.Should().Be(2); // initial + re-login
        queryCalls.Should().Be(2); // 401 then success
    }

    [Fact]
    public async Task GetAsync_401AndReLoginReturns401_ThrowsSapAuthenticationException()
    {
        var pwd = CreateEnvVar();
        var loginCalls = 0;

        var (client, _) = Build(pwd, req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/Login"))
            {
                loginCalls++;
                return loginCalls == 1
                    ? LoginOk()
                    : Status(HttpStatusCode.Unauthorized);
            }
            return Status(HttpStatusCode.Unauthorized);
        });

        await client.LoginAsync();
        var act = async () => await client.GetExistingUserTablesAsync(new[] { "X" });

        await act.Should().ThrowAsync<SapAuthenticationException>();
    }

    [Fact]
    public async Task GetAsync_401AndPersists_ThrowsSapAuthenticationException()
    {
        var pwd = CreateEnvVar();
        var (client, _) = Build(pwd, req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/Login"))
                return LoginOk();
            return Status(HttpStatusCode.Unauthorized);
        });

        await client.LoginAsync();
        var act = async () => await client.GetExistingUserTablesAsync(new[] { "X" });

        var ex = (await act.Should().ThrowAsync<SapAuthenticationException>()).Which;
        ex.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    // ---------- Login resilience ----------

    [Fact]
    public async Task LoginAsync_401_ThrowsSapAuthenticationExceptionWithoutRetry()
    {
        var pwd = CreateEnvVar();
        var calls = 0;
        var (client, _) = Build(pwd, _ =>
        {
            calls++;
            return Status(HttpStatusCode.Unauthorized,
                "{\"error\":{\"code\":\"-2028\",\"message\":{\"value\":\"User code or password is incorrect\"}}}");
        });

        var act = async () => await client.LoginAsync();

        var ex = (await act.Should().ThrowAsync<SapAuthenticationException>()).Which;
        ex.SapErrorCode.Should().Be("-2028");
        ex.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
        calls.Should().Be(1);
    }

    [Fact]
    public async Task LoginAsync_503ThenSuccess_RetriesAndCompletes()
    {
        var pwd = CreateEnvVar();
        var calls = 0;
        var (client, _) = Build(pwd, _ =>
        {
            calls++;
            return calls == 1 ? Status(HttpStatusCode.ServiceUnavailable) : LoginOk();
        });

        await client.LoginAsync();

        client.HasActiveSession.Should().BeTrue();
        calls.Should().Be(2);
    }

    [Fact]
    public async Task LoginAsync_400_ThrowsSapFunctionalException()
    {
        var pwd = CreateEnvVar();
        var (client, _) = Build(pwd, _ =>
            Status(HttpStatusCode.BadRequest,
                "{\"error\":{\"code\":\"301\",\"message\":{\"value\":\"Bad payload\"}}}"));

        var act = async () => await client.LoginAsync();

        var ex = (await act.Should().ThrowAsync<SapFunctionalException>()).Which;
        ex.SapErrorCode.Should().Be("301");
    }

    // ---------- Logout best-effort ----------

    [Fact]
    public async Task LogoutAsync_500_SwallowsAndClearsSession()
    {
        var pwd = CreateEnvVar();
        var (client, _) = Build(pwd, req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/Login")) return LoginOk();
            return Status(HttpStatusCode.InternalServerError);
        }, maxRetries: 0);

        await client.LoginAsync();

        var act = async () => await client.LogoutAsync();
        await act.Should().ThrowAsync<SapTransientException>();
        client.HasActiveSession.Should().BeFalse();
    }

    [Fact]
    public async Task LogoutAsync_NoSession_NoOp()
    {
        var pwd = CreateEnvVar();
        var (client, handler) = Build(pwd, _ => LoginOk());

        await client.LogoutAsync();

        handler.Requests.Should().BeEmpty();
        client.HasActiveSession.Should().BeFalse();
    }

    // ---------- Functional exception sanitization ----------

    [Fact]
    public async Task SapFunctionalException_SanitizesCrLfAndTruncatesLongMessages()
    {
        var pwd = CreateEnvVar();
        var longMessage = new string('x', 1500);
        var bodyJson = "{\"error\":{\"code\":\"42\",\"message\":{\"value\":\"line1\\r\\nline2\\t\\t" + longMessage + "\"}}}";

        var client = await LoggedInClient(pwd, _ => Status(HttpStatusCode.BadRequest, bodyJson));

        var act = async () => await client.GetExistingUserTablesAsync(new[] { "X" });
        var ex = (await act.Should().ThrowAsync<SapFunctionalException>()).Which;

        ex.SapErrorMessage.Should().NotContain("\r");
        ex.SapErrorMessage.Should().NotContain("\n");
        ex.SapErrorMessage.Should().NotContain("\t");
        ex.SapErrorMessage!.Length.Should().BeLessThanOrEqualTo(1100);
        ex.SapErrorMessage.Should().EndWith("[truncated]");
    }

    [Fact]
    public async Task SapFunctionalException_NonJsonBody_PreservedAsRawSanitized()
    {
        var pwd = CreateEnvVar();
        var client = await LoggedInClient(pwd, _ =>
            Status(HttpStatusCode.BadRequest, "Plain text\r\nstack trace info"));

        var act = async () => await client.GetExistingUserTablesAsync(new[] { "X" });
        var ex = (await act.Should().ThrowAsync<SapFunctionalException>()).Which;

        ex.SapErrorCode.Should().BeNull();
        ex.SapErrorMessage.Should().NotContain("\r");
        ex.SapErrorMessage.Should().NotContain("\n");
        ex.SapErrorMessage.Should().Contain("Plain text");
    }

    // ---------- CorrelationId propagation ----------

    [Fact]
    public async Task SapException_PropagatesCorrelationId()
    {
        var pwd = CreateEnvVar();
        var (client, _) = Build(pwd, _ => Status(HttpStatusCode.Unauthorized));
        client.CorrelationId = "run-123";

        var act = async () => await client.LoginAsync();
        var ex = (await act.Should().ThrowAsync<SapAuthenticationException>()).Which;

        ex.CorrelationId.Should().Be("run-123");
    }

    // ---------- Retry exhaustion ----------

    [Fact]
    public async Task GetAsync_RetryExhausted_AttemptsMadeMatchesMaxRetriesPlusOne()
    {
        var pwd = CreateEnvVar();
        var calls = 0;
        var client = await LoggedInClient(pwd, req =>
        {
            if (req.RequestUri!.AbsolutePath.EndsWith("/UserTablesMD"))
            {
                calls++;
                return Status(HttpStatusCode.BadGateway);
            }
            return Status(HttpStatusCode.NotFound);
        }, maxRetries: 2);

        var act = async () => await client.GetExistingUserTablesAsync(new[] { "X" });
        var ex = (await act.Should().ThrowAsync<SapTransientException>()).Which;

        ex.AttemptsMade.Should().Be(3); // 1 initial + 2 retries
        calls.Should().Be(3);
    }

    // ---------- FileLogger resilience ----------

    [Fact]
    public void FileLogger_AfterDirectoryDeleted_DoesNotThrow()
    {
        var dir = Path.Combine(Path.GetTempPath(), $"sdc_log_test_{Guid.NewGuid():N}");
        var logger = new SmartDocControl.Infrastructure.Logging.FileLogger(
            new LoggingOptions { LogPath = dir, PendingFunctionalLogPath = dir },
            "test-cid");

        logger.Information("first");
        Directory.Delete(dir, true);

        Action act = () =>
        {
            logger.Information("second");
            logger.Warning("third");
            logger.Error("fourth", new InvalidOperationException("boom"));
        };

        act.Should().NotThrow();
    }
}
