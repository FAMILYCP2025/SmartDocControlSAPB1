namespace SmartDocControl.Infrastructure.Configuration;

public sealed class SapOptions
{
    public string BaseUrl { get; set; } = string.Empty;
    public string CompanyDb { get; set; } = string.Empty;
    public string Username { get; set; } = string.Empty;
    public string PasswordEnvironmentVariable { get; set; } = string.Empty;
    public bool IgnoreSslErrors { get; set; }
    public int TimeoutSeconds { get; set; } = 60;
}
