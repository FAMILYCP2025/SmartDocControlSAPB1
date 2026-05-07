using System.Text.Json;
using SmartDocControl.Infrastructure.Configuration;

namespace SmartDocControl.Infrastructure.Logging;

public sealed class FileLogger
{
    private readonly string _logFilePath;
    private readonly string _correlationId;
    private readonly bool _debugMode;

    public FileLogger(LoggingOptions options, string correlationId)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (string.IsNullOrWhiteSpace(correlationId))
            throw new ArgumentException("CorrelationId is required.", nameof(correlationId));

        _correlationId = correlationId;
        _debugMode = options.DebugMode;

        Directory.CreateDirectory(options.LogPath);
        _logFilePath = Path.Combine(options.LogPath, $"smartdoc_{DateTime.UtcNow:yyyyMMdd}.log");
    }

    public void Information(string message) => Append("INF", message);
    public void Warning(string message) => Append("WRN", message);
    public void Error(string message, Exception? ex = null) => Append("ERR", message, ex);
    public void Debug(string message) { if (_debugMode) Append("DBG", message); }

    public Task InformationAsync(string message) => AppendAsync("INF", message);
    public Task WarningAsync(string message) => AppendAsync("WRN", message);
    public Task ErrorAsync(string message, Exception? ex = null) => AppendAsync("ERR", message, ex);
    public Task DebugAsync(string message) => _debugMode ? AppendAsync("DBG", message) : Task.CompletedTask;

    private void Append(string level, string message, Exception? ex = null)
    {
        try
        {
            File.AppendAllText(_logFilePath, Serialize(level, message, ex));
        }
        catch (Exception ioEx)
        {
            TryWriteToConsoleFallback(level, message, ex, ioEx);
        }
    }

    private async Task AppendAsync(string level, string message, Exception? ex = null)
    {
        try
        {
            await File.AppendAllTextAsync(_logFilePath, Serialize(level, message, ex));
        }
        catch (Exception ioEx)
        {
            TryWriteToConsoleFallback(level, message, ex, ioEx);
        }
    }

    private void TryWriteToConsoleFallback(string level, string message, Exception? originalEx, Exception fileEx)
    {
        try
        {
            var fallback = $"[{DateTime.UtcNow:o}] [{level}] [cid={_correlationId}] [LOGFILE_FAILED:{fileEx.GetType().Name}] {message}";
            if (originalEx is not null)
                fallback += $" | EX: {originalEx.Message}";
            Console.Error.WriteLine(fallback);
        }
        catch
        {
            // Final swallow: logging must never break the process flow.
        }
    }

    private string Serialize(string level, string message, Exception? ex)
    {
        var ts = DateTime.UtcNow.ToString("o");

        if (ex is null)
            return JsonSerializer.Serialize(new { ts, lvl = level, cid = _correlationId, msg = message })
                   + Environment.NewLine;

        return JsonSerializer.Serialize(new { ts, lvl = level, cid = _correlationId, msg = message, ex = ex.ToString() })
               + Environment.NewLine;
    }
}
