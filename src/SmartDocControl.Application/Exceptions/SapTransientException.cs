using System.Net;

namespace SmartDocControl.Application.Exceptions;

public sealed class SapTransientException : Exception
{
    public HttpStatusCode? StatusCode { get; }
    public int AttemptsMade { get; }
    public string? CorrelationId { get; }

    public SapTransientException(
        string message,
        HttpStatusCode? statusCode,
        int attemptsMade,
        string? correlationId = null,
        Exception? innerException = null)
        : base(message, innerException)
    {
        StatusCode = statusCode;
        AttemptsMade = attemptsMade;
        CorrelationId = correlationId;
    }
}
