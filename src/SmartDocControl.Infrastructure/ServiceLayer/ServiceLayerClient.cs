using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Text.Json;
using SmartDocControl.Application.Exceptions;
using SmartDocControl.Infrastructure.Configuration;
using SmartDocControl.Infrastructure.ServiceLayer.Dtos;

namespace SmartDocControl.Infrastructure.ServiceLayer;

public sealed class ServiceLayerClient
{
    private static readonly HashSet<HttpStatusCode> TransientStatusCodes = new()
    {
        HttpStatusCode.RequestTimeout,        // 408
        HttpStatusCode.TooManyRequests,       // 429
        HttpStatusCode.InternalServerError,   // 500
        HttpStatusCode.BadGateway,            // 502
        HttpStatusCode.ServiceUnavailable,    // 503
        HttpStatusCode.GatewayTimeout         // 504
    };

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    private readonly HttpClient _httpClient;
    private readonly SapOptions _options;
    private readonly ExecutionOptions _executionOptions;
    private volatile ServiceLayerSession? _session;

    public bool HasActiveSession => _session != null;

    public string? CorrelationId { get; set; }

    public ServiceLayerClient(HttpClient httpClient, SapOptions options, ExecutionOptions executionOptions)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(executionOptions);

        if (httpClient.BaseAddress is null)
            throw new ArgumentException(
                "HttpClient.BaseAddress must be configured before constructing ServiceLayerClient.",
                nameof(httpClient));

        if (!httpClient.BaseAddress.AbsoluteUri.EndsWith("/"))
            httpClient.BaseAddress = new Uri(httpClient.BaseAddress.AbsoluteUri + "/");

        _httpClient = httpClient;
        _options = options;
        _executionOptions = executionOptions;
    }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        var password = Environment.GetEnvironmentVariable(_options.PasswordEnvironmentVariable)
            ?? throw new InvalidOperationException(
                $"Password environment variable '{_options.PasswordEnvironmentVariable}' is not set.");

        var loginPayload = JsonSerializer.Serialize(new SapLoginRequest
        {
            CompanyDB = _options.CompanyDb,
            UserName = _options.Username,
            Password = password
        });

        var response = await SendWithTransientRetryAsync(
            () => new HttpRequestMessage(HttpMethod.Post, "Login")
            {
                Content = new StringContent(loginPayload, Encoding.UTF8, "application/json")
            },
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var sapError = TryParseSapError(errorBody);

                if (response.StatusCode == HttpStatusCode.Unauthorized
                    || response.StatusCode == HttpStatusCode.Forbidden)
                {
                    throw new SapAuthenticationException(
                        $"SAP login failed with HTTP {(int)response.StatusCode}.",
                        statusCode: response.StatusCode,
                        sapErrorCode: sapError?.code,
                        correlationId: CorrelationId);
                }

                throw new SapFunctionalException(
                    response.StatusCode,
                    sapError?.code,
                    sapError?.message ?? errorBody,
                    requestUrl: "Login",
                    correlationId: CorrelationId);
            }
        }

        try
        {
            var sessionId = ExtractCookieValue(response, "B1SESSION")
                ?? throw new InvalidOperationException("SAP Service Layer did not return a B1SESSION cookie.");
            var routeId = ExtractCookieValue(response, "ROUTEID");

            var newSession = new ServiceLayerSession(sessionId, routeId);
            Interlocked.Exchange(ref _session, newSession);
        }
        finally
        {
            response.Dispose();
        }
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null) return;

        try
        {
            var response = await SendWithTransientRetryAsync(
                () => CreateAuthenticatedRequest(HttpMethod.Post, "Logout"),
                cancellationToken);
            response.Dispose();
        }
        finally
        {
            Interlocked.Exchange(ref _session, null);
        }
    }

    public async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken = default)
    {
        if (_session is null)
            throw new InvalidOperationException("No active SAP session. Call LoginAsync first.");

        var response = await SendAuthenticatedAsync(
            () => CreateAuthenticatedRequest(HttpMethod.Get, relativeUrl),
            cancellationToken);

        if (!response.IsSuccessStatusCode)
        {
            using (response)
            {
                var errorBody = await response.Content.ReadAsStringAsync(cancellationToken);
                var sapError = TryParseSapError(errorBody);
                throw new SapFunctionalException(
                    response.StatusCode,
                    sapError?.code,
                    sapError?.message ?? errorBody,
                    requestUrl: relativeUrl,
                    correlationId: CorrelationId);
            }
        }

        using (response)
        {
            var json = await response.Content.ReadAsStringAsync(cancellationToken);
            return JsonSerializer.Deserialize<T>(json, JsonOptions)
                ?? throw new InvalidOperationException($"Failed to deserialize response from '{relativeUrl}'.");
        }
    }

    public async Task<IReadOnlySet<string>> GetExistingUserTablesAsync(
        IEnumerable<string> tableNames,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(tableNames);
        if (_session is null)
            throw new InvalidOperationException("No active SAP session. Call LoginAsync first.");

        var names = tableNames
            .Where(n => !string.IsNullOrWhiteSpace(n))
            .Select(n => n.Trim())
            .ToList();

        if (names.Count == 0)
            return new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        var filterExpression = string.Join(" or ", names.Select(n => $"TableName eq '{n}'"));
        var url = $"UserTablesMD?$filter={Uri.EscapeDataString(filterExpression)}&$select=TableName";

        var result = await GetAsync<SapPagedResult<SapUserTableDto>>(url, cancellationToken);

        var existing = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var dto in result.Value)
        {
            if (!string.IsNullOrWhiteSpace(dto.TableName))
                existing.Add(dto.TableName);
        }
        return existing;
    }

    private async Task<HttpResponseMessage> SendAuthenticatedAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        var response = await SendWithTransientRetryAsync(requestFactory, cancellationToken);

        if (response.StatusCode != HttpStatusCode.Unauthorized)
            return response;

        // 401: invalidate session, re-login once, retry once
        response.Dispose();
        Interlocked.Exchange(ref _session, null);

        await LoginAsync(cancellationToken);

        var retryResponse = await SendWithTransientRetryAsync(requestFactory, cancellationToken);

        if (retryResponse.StatusCode == HttpStatusCode.Unauthorized)
        {
            retryResponse.Dispose();
            throw new SapAuthenticationException(
                "Still unauthorized after re-login. Session may be invalid or credentials revoked.",
                statusCode: HttpStatusCode.Unauthorized,
                correlationId: CorrelationId);
        }

        return retryResponse;
    }

    private async Task<HttpResponseMessage> SendWithTransientRetryAsync(
        Func<HttpRequestMessage> requestFactory,
        CancellationToken cancellationToken)
    {
        var maxAttempts = Math.Max(1, _executionOptions.MaxRetries + 1);
        Exception? lastException = null;
        HttpStatusCode? lastStatusCode = null;
        var attempt = 0;

        while (true)
        {
            attempt++;
            cancellationToken.ThrowIfCancellationRequested();

            try
            {
                var request = requestFactory();
                var response = await _httpClient.SendAsync(request, cancellationToken);

                if (response.IsSuccessStatusCode || !TransientStatusCodes.Contains(response.StatusCode))
                    return response;

                lastStatusCode = response.StatusCode;
                lastException = null;
                response.Dispose();
            }
            catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
            {
                throw; // user cancellation propagates
            }
            catch (TaskCanceledException ex)
            {
                // HttpClient timeout - transient
                lastException = ex;
                lastStatusCode = null;
            }
            catch (HttpRequestException ex) when (IsTransientNetworkError(ex))
            {
                lastException = ex;
                lastStatusCode = null;
            }

            if (attempt >= maxAttempts)
            {
                throw new SapTransientException(
                    $"Transient failure after {attempt} attempt(s).",
                    lastStatusCode,
                    attempt,
                    correlationId: CorrelationId,
                    innerException: lastException);
            }

            var delaySeconds = Math.Pow(2, attempt - 1) * _executionOptions.RetryDelaySeconds;
            if (delaySeconds > 0)
                await Task.Delay(TimeSpan.FromSeconds(delaySeconds), cancellationToken);
        }
    }

    private static bool IsTransientNetworkError(HttpRequestException ex)
    {
        if (ex.InnerException is SocketException socketEx)
        {
            return socketEx.SocketErrorCode == SocketError.ConnectionReset
                || socketEx.SocketErrorCode == SocketError.ConnectionAborted;
        }
        return false;
    }

    private static (string? code, string? message)? TryParseSapError(string body)
    {
        if (string.IsNullOrWhiteSpace(body)) return null;

        try
        {
            var envelope = JsonSerializer.Deserialize<SapErrorEnvelopeDto>(body, JsonOptions);
            if (envelope?.Error is null) return null;
            return (envelope.Error.Code, envelope.Error.Message?.Value);
        }
        catch
        {
            return null;
        }
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string relativeUrl)
    {
        var session = _session
            ?? throw new InvalidOperationException("No active SAP session.");

        var request = new HttpRequestMessage(method, relativeUrl);

        var cookieValue = $"B1SESSION={session.SessionId}";
        if (session.RouteId is not null)
            cookieValue += $"; ROUTEID={session.RouteId}";

        request.Headers.Add("Cookie", cookieValue);
        return request;
    }

    private static string? ExtractCookieValue(HttpResponseMessage response, string cookieName)
    {
        if (!response.Headers.TryGetValues("Set-Cookie", out var setCookieHeaders))
            return null;

        var prefix = $"{cookieName}=";
        foreach (var header in setCookieHeaders)
        {
            if (header.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                var value = header[prefix.Length..];
                var semicolonIndex = value.IndexOf(';');
                return semicolonIndex >= 0 ? value[..semicolonIndex] : value;
            }
        }

        return null;
    }
}
