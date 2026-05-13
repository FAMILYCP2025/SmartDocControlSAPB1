using System.Text;

namespace SmartDocControl.Schema.Sap;

/// <summary>
/// Traces SAP Service Layer HTTP requests to a caller-supplied sink.
/// Sensitive headers (Cookie, Authorization, B1SESSION, ROUTEID) are omitted.
/// </summary>
public sealed class MetadataTraceHandler : DelegatingHandler
{
    private static readonly HashSet<string> SensitiveHeaders =
        new(StringComparer.OrdinalIgnoreCase)
        {
            "Cookie", "Authorization", "B1SESSION", "ROUTEID"
        };

    private readonly Action<string> _onTrace;

    public MetadataTraceHandler(Action<string> onTrace)
    {
        ArgumentNullException.ThrowIfNull(onTrace);
        _onTrace = onTrace;
    }

    protected override async Task<HttpResponseMessage> SendAsync(
        HttpRequestMessage request,
        CancellationToken cancellationToken)
    {
        var sb = new StringBuilder();
        sb.Append($"[TRACE] {request.Method} {request.RequestUri}");

        foreach (var (key, values) in request.Headers)
        {
            if (!SensitiveHeaders.Contains(key))
                sb.Append($"\n  {key}: {string.Join(", ", values)}");
        }

        if (request.Content is not null)
        {
            await request.Content.LoadIntoBufferAsync().ConfigureAwait(false);
            var body = await request.Content.ReadAsStringAsync(cancellationToken).ConfigureAwait(false);
            if (!string.IsNullOrEmpty(body))
                sb.Append($"\n  Body: {body}");
        }

        _onTrace(sb.ToString());

        return await base.SendAsync(request, cancellationToken).ConfigureAwait(false);
    }
}
