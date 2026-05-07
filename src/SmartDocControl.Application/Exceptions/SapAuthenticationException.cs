using System.Net;

namespace SmartDocControl.Application.Exceptions;

public sealed class SapAuthenticationException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public string? SapErrorCode { get; }
    public string? CorrelationId { get; }

    public SapAuthenticationException(
        string message,
        HttpStatusCode? statusCode = null,
        string? sapErrorCode = null,
        string? correlationId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        SapErrorCode = sapErrorCode;
        CorrelationId = correlationId;
    }
}
