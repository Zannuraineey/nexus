using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Domain.Common;

/// <summary>
/// Standard immutable operation result implementation.
/// </summary>
public sealed class OperationResult : IOperationResult
{
    public bool IsSuccess { get; }
    public OperationStatus Status { get; }
    public string Message { get; }
    public string? ErrorDetails { get; }
    public TimeSpan ElapsedDuration { get; }
    public IReadOnlyList<string> LogLines { get; }

    private OperationResult(
        bool isSuccess,
        OperationStatus status,
        string message,
        string? errorDetails,
        TimeSpan elapsedDuration,
        IReadOnlyList<string>? logLines)
    {
        IsSuccess = isSuccess;
        Status = status;
        Message = message;
        ErrorDetails = errorDetails;
        ElapsedDuration = elapsedDuration;
        LogLines = logLines ?? Array.Empty<string>();
    }

    public static OperationResult Success(
        string message = "Operation completed successfully.",
        TimeSpan elapsed = default,
        IReadOnlyList<string>? logLines = null) =>
        new(true, OperationStatus.Completed, message, null, elapsed, logLines);

    public static OperationResult Failure(
        string message,
        string? errorDetails = null,
        TimeSpan elapsed = default,
        IReadOnlyList<string>? logLines = null) =>
        new(false, OperationStatus.Failed, message, errorDetails, elapsed, logLines);

    public static OperationResult Cancelled(
        string message = "Operation was cancelled by user.",
        TimeSpan elapsed = default,
        IReadOnlyList<string>? logLines = null) =>
        new(false, OperationStatus.Cancelled, message, null, elapsed, logLines);
}

/// <summary>
/// Standard typed operation result implementation.
/// </summary>
public sealed class OperationResult<TData> : IOperationResult<TData>
{
    public bool IsSuccess { get; }
    public OperationStatus Status { get; }
    public string Message { get; }
    public string? ErrorDetails { get; }
    public TimeSpan ElapsedDuration { get; }
    public IReadOnlyList<string> LogLines { get; }
    public TData? Data { get; }

    private OperationResult(
        bool isSuccess,
        OperationStatus status,
        string message,
        string? errorDetails,
        TimeSpan elapsedDuration,
        IReadOnlyList<string>? logLines,
        TData? data)
    {
        IsSuccess = isSuccess;
        Status = status;
        Message = message;
        ErrorDetails = errorDetails;
        ElapsedDuration = elapsedDuration;
        LogLines = logLines ?? Array.Empty<string>();
        Data = data;
    }

    public static OperationResult<TData> Success(
        TData data,
        string message = "Operation completed successfully.",
        TimeSpan elapsed = default,
        IReadOnlyList<string>? logLines = null) =>
        new(true, OperationStatus.Completed, message, null, elapsed, logLines, data);

    public static OperationResult<TData> Failure(
        string message,
        string? errorDetails = null,
        TimeSpan elapsed = default,
        IReadOnlyList<string>? logLines = null) =>
        new(false, OperationStatus.Failed, message, errorDetails, elapsed, logLines, default);

    public static OperationResult<TData> Cancelled(
        string message = "Operation was cancelled by user.",
        TimeSpan elapsed = default,
        IReadOnlyList<string>? logLines = null) =>
        new(false, OperationStatus.Cancelled, message, null, elapsed, logLines, default);
}
