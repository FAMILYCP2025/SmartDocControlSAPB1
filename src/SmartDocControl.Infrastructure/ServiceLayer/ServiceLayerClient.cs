using System.Text;
using System.Text.Json;
using SmartDocControl.Infrastructure.Configuration;
using SmartDocControl.Infrastructure.ServiceLayer.Dtos;

namespace SmartDocControl.Infrastructure.ServiceLayer;

public sealed class ServiceLayerClient
{
    private readonly HttpClient _httpClient;
    private readonly SapOptions _options;
    private ServiceLayerSession? _session;

    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNameCaseInsensitive = true
    };

    public bool HasActiveSession => _session != null;

    public ServiceLayerClient(HttpClient httpClient, SapOptions options)
    {
        ArgumentNullException.ThrowIfNull(httpClient);
        ArgumentNullException.ThrowIfNull(options);
        _httpClient = httpClient;
        _options = options;
    }

    public async Task LoginAsync(CancellationToken cancellationToken = default)
    {
        var password = Environment.GetEnvironmentVariable(_options.PasswordEnvironmentVariable)
            ?? throw new InvalidOperationException(
                $"Password environment variable '{_options.PasswordEnvironmentVariable}' is not set.");

        var loginRequest = new SapLoginRequest
        {
            CompanyDB = _options.CompanyDb,
            UserName = _options.Username,
            Password = password
        };

        var content = new StringContent(
            JsonSerializer.Serialize(loginRequest),
            Encoding.UTF8,
            "application/json");

        var response = await _httpClient.PostAsync("Login", content, cancellationToken);
        response.EnsureSuccessStatusCode();

        var sessionId = ExtractCookieValue(response, "B1SESSION")
            ?? throw new InvalidOperationException("SAP Service Layer did not return a B1SESSION cookie.");
        var routeId = ExtractCookieValue(response, "ROUTEID");

        _session = new ServiceLayerSession(sessionId, routeId);
    }

    public async Task LogoutAsync(CancellationToken cancellationToken = default)
    {
        if (_session is null) return;

        try
        {
            var request = CreateAuthenticatedRequest(HttpMethod.Post, "Logout");
            await _httpClient.SendAsync(request, cancellationToken);
        }
        finally
        {
            _session = null;
        }
    }

    public async Task<T> GetAsync<T>(string relativeUrl, CancellationToken cancellationToken = default)
    {
        if (_session is null)
            throw new InvalidOperationException("No active SAP session. Call LoginAsync first.");

        var request = CreateAuthenticatedRequest(HttpMethod.Get, relativeUrl);
        var response = await _httpClient.SendAsync(request, cancellationToken);
        response.EnsureSuccessStatusCode();

        var json = await response.Content.ReadAsStringAsync(cancellationToken);
        return JsonSerializer.Deserialize<T>(json, JsonOptions)
            ?? throw new InvalidOperationException($"Failed to deserialize response from '{relativeUrl}'.");
    }

    private HttpRequestMessage CreateAuthenticatedRequest(HttpMethod method, string relativeUrl)
    {
        if (_session is null)
            throw new InvalidOperationException("No active SAP session.");

        var request = new HttpRequestMessage(method, relativeUrl);

        var cookieValue = $"B1SESSION={_session.SessionId}";
        if (_session.RouteId is not null)
            cookieValue += $"; ROUTEID={_session.RouteId}";

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
