using System.Net;
using System.Text;

namespace SmartDocControl.Schema.Sap;

/// <summary>
/// Traces SAP Service Layer HTTP requests to a caller-supplied sink.
/// Sensitive headers (Cookie, Authorization, B1SESSION, ROUTEID) are omitted.
/// When a CookieContainer is supplied, presence of session cookies is reported
/// by name and count only — values are never logged.
/// </summary>
public sealed class MetadataTraceHandler : DelegatingHandler
{
    private static readonly HashSet<string> SensitiveHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Cookie", "Authorization", "B1SESSION", "ROUTEID"
        };

    private readonly Action<string> _onTrace;
    private readonly CookieContainer? _cookieContainer;

    public MetadataTraceHandler(Action<string> onTrace, CookieContainer? cookieContainer = null)
    {
        ArgumentNullException.ThrowIfNull(onTrace);
        _onTrace = onTrace;
        _cookieContainer = cookieContainer;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var isLogin = request.RequestUri?.AbsolutePath
            .EndsWith("/Login", StringComparison.OrdinalIgnoreCase) ?? false;
        var isUdfPost = request.Method == HttpMethod.Post &&
            (request.RequestUri?.AbsolutePath
                .EndsWith("/UserFieldsMD", StringComparison.OrdinalIgnoreCase) ?? false);

        var sb = new StringBuilder();

        if (isUdfPost)
            sb.AppendLine(">>> UDF POST <<<");

        sb.Append($"[TRACE] {request.Method} {request.RequestUri}");

        foreach (var (key, values) in request.Headers)
        {
            if (!SensitiveHeaders.Contains(key))
                sb.Append($"\n  {key}: {string.Join(", ", values)}");
        }

        if (request.Content is not null)
        {
            var contentType = request.Content.Headers.ContentType?.ToString();
            if (!string.IsNullOrEmpty(contentType))
                sb.Append($"\n  Content-Type: {contentType}");

            if (isLogin)
            {
                sb.Append("\n  Body: [suppressed — login credentials]");
            }
            else
            {
                await request.Content.LoadIntoBufferAsync().ConfigureAwait(false);
                var body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
                if (!string.IsNullOrEmpty(body))
                    sb.Append($"\n  Body: {body}");
            }
        }

        AppendCookieDiagnostic(sb, request);

        _onTrace(sb.ToString());

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }

    private void AppendCookieDiagnostic(StringBuilder sb, HttpRequestMessage request)
    {
        if (request.RequestUri is null)
            return;

        if (_cookieContainer is not null)
        {
            var cookies = _cookieContainer.GetCookies(request.RequestUri);
            int b1Count = 0;
            int routeCount = 0;
            int otherCount = 0;
            foreach (Cookie cookie in cookies)
            {
                if (string.Equals(cookie.Name, "B1SESSION", StringComparison.OrdinalIgnoreCase))
                    b1Count++;
                else if (string.Equals(cookie.Name, "ROUTEID", StringComparison.OrdinalIgnoreCase))
                    routeCount++;
                else
                    otherCount++;
            }

            var hostDisplay = request.RequestUri.IsDefaultPort
                ? $"{request.RequestUri.Scheme}://{request.RequestUri.Host}"
                : $"{request.RequestUri.Scheme}://{request.RequestUri.Host}:{request.RequestUri.Port}";

            sb.Append($"\n  CookieContainer for {hostDisplay}/:");
            sb.Append($"\n    B1SESSION: {(b1Count > 0 ? "present" : "absent")} (count={b1Count})");
            sb.Append($"\n    ROUTEID:   {(routeCount > 0 ? "present" : "absent")} (count={routeCount})");
            sb.Append($"\n    other:     count={otherCount}");
        }

        if (request.Headers.TryGetValues("Cookie", out var cookieHeaderValues))
        {
            var names = new List<string>();
            foreach (var headerValue in cookieHeaderValues)
            {
                foreach (var pair in headerValue.Split(';'))
                {
                    var trimmed = pair.Trim();
                    if (trimmed.Length == 0)
                        continue;
                    var eqIdx = trimmed.IndexOf('=');
                    var name = eqIdx > 0 ? trimmed[..eqIdx].Trim() : trimmed;
                    if (name.Length > 0)
                        names.Add(name);
                }
            }
            sb.Append($"\n  Request manual Cookie header: present, names=[{string.Join(", ", names)}]");
        }
        else
        {
            sb.Append("\n  Request manual Cookie header: absent");
        }
    }
}
