namespace SmartDocControl.Infrastructure.ServiceLayer;

public sealed class ServiceLayerSession
{
    public string SessionId { get; }
    public string? RouteId { get; }
    public DateTime CreatedAt { get; }

    public ServiceLayerSession(string sessionId, string? routeId)
    {
        if (string.IsNullOrWhiteSpace(sessionId))
            throw new ArgumentException("SessionId is required.", nameof(sessionId));

        SessionId = sessionId;
        RouteId = routeId;
        CreatedAt = DateTime.UtcNow;
    }
}
