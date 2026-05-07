using System.Net;

namespace SmartDocControl.Application.Exceptions;

public sealed class SapFunctionalException : Exception
{
    private const int MaxMessageLength = 1000;

    public HttpStatusCode StatusCode { get; }
    public string? SapErrorCode { get; }
    public string? SapErrorMessage { get; }
    public string? RequestUrl { get; }
    public string? CorrelationId { get; }

    public SapFunctionalException(
        HttpStatusCode statusCode,
        string? sapErrorCode,
        string? sapErrorMessage,
        string? requestUrl,
        string? correlationId = null,
        Exception? innerException = null)
        : base(BuildMessage(statusCode, sapErrorCode, Sanitize(sapErrorMessage), requestUrl), innerException)
    {
        StatusCode = statusCode;
        SapErrorCode = sapErrorCode;
        SapErrorMessage = Sanitize(sapErrorMessage);
        RequestUrl = requestUrl;
        CorrelationId = correlationId;
    }

    private static string BuildMessage(HttpStatusCode status, string? code, string sanitizedBody, string? url)
    {
        var codePart = string.IsNullOrEmpty(code) ? string.Empty : $" [{code}]";
        var urlPart = string.IsNullOrEmpty(url) ? string.Empty : $" (url: {url})";
        var bodyPart = string.IsNullOrEmpty(sanitizedBody) ? string.Empty : $": {sanitizedBody}";
        return $"SAP functional error {(int)status}{codePart}{bodyPart}{urlPart}";
    }

    private static string Sanitize(string? input)
    {
        if (string.IsNullOrEmpty(input)) return string.Empty;

        var s = input.Replace('\r', ' ').Replace('\n', ' ').Replace('\t', ' ');
        while (s.Contains("  "))
            s = s.Replace("  ", " ");
        s = s.Trim();

        if (s.Length > MaxMessageLength)
            s = s.Substring(0, MaxMessageLength) + "...[truncated]";

        return s;
    }
}
