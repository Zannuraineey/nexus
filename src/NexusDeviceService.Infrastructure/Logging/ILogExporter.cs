using System.Text.Json;
using Microsoft.Extensions.Logging;

namespace NexusDeviceService.Infrastructure.Logging;

public enum LogExportFormat
{
    Text,
    Json
}

public sealed record LogExportResult
{
    public bool IsSuccess { get; init; }
    public string? FilePath { get; init; }
    public string? ErrorMessage { get; init; }
    public int ExportedCount { get; init; }

    public static LogExportResult Succeeded(string path, int count) =>
        new() { IsSuccess = true, FilePath = path, ExportedCount = count };

    public static LogExportResult Failed(string error) =>
        new() { IsSuccess = false, ErrorMessage = error };
}

public interface ILogExporter
{
    Task<LogExportResult> ExportLogsAsync(
        IReadOnlyList<AppLogEntry> entries,
        string targetFilePath,
        LogExportFormat format = LogExportFormat.Text,
        CancellationToken cancellationToken = default);
}

public sealed class FileLogExporter : ILogExporter
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true
    };

    private readonly ILogger<FileLogExporter> _logger;

    public FileLogExporter(ILogger<FileLogExporter> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LogExportResult> ExportLogsAsync(
        IReadOnlyList<AppLogEntry> entries,
        string targetFilePath,
        LogExportFormat format = LogExportFormat.Text,
        CancellationToken cancellationToken = default)
    {
        if (entries == null || entries.Count == 0)
        {
            return LogExportResult.Failed("No log entries available to export.");
        }

        if (string.IsNullOrWhiteSpace(targetFilePath))
        {
            return LogExportResult.Failed("Target file path cannot be empty.");
        }

        try
        {
            var dir = Path.GetDirectoryName(targetFilePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            if (format == LogExportFormat.Json)
            {
                var json = JsonSerializer.Serialize(entries, JsonOptions);
                await File.WriteAllTextAsync(targetFilePath, json, cancellationToken).ConfigureAwait(false);
            }
            else
            {
                var lines = entries.Select(e =>
                    $"[{e.Timestamp:yyyy-MM-dd HH:mm:ss.fff}] [{e.Level,-11}] [{e.Category}] {(e.CorrelationId != null ? $"[{e.CorrelationId}] " : "")}{e.Message}{(e.Exception != null ? $"\nException: {e.Exception}" : "")}");

                await File.WriteAllLinesAsync(targetFilePath, lines, cancellationToken).ConfigureAwait(false);
            }

            _logger.LogInformation("Exported {Count} log entries to {Path} ({Format})", entries.Count, targetFilePath, format);
            return LogExportResult.Succeeded(targetFilePath, entries.Count);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to export logs to {Path}", targetFilePath);
            return LogExportResult.Failed($"Export failed: {ex.Message}");
        }
    }
}
