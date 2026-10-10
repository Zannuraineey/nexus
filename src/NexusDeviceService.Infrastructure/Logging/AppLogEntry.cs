using Microsoft.Extensions.Logging;

namespace NexusDeviceService.Infrastructure.Logging;

/// <summary>
/// Structured application log entry carrying timestamp, level, category, scrubbed message,
/// correlation ID, and optional exception data.
/// </summary>
public sealed record AppLogEntry(
    DateTime Timestamp,
    LogLevel Level,
    string Category,
    string Message,
    string? CorrelationId = null,
    Exception? Exception = null);
