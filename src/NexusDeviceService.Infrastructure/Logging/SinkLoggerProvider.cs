using Microsoft.Extensions.Logging;

namespace NexusDeviceService.Infrastructure.Logging;

public sealed class SinkLoggerProvider : ILoggerProvider
{
    private readonly IAppLogSink _sink;
    private readonly ISensitiveDataRedactor? _redactor;

    public SinkLoggerProvider(IAppLogSink sink, ISensitiveDataRedactor? redactor = null)
    {
        _sink = sink ?? throw new ArgumentNullException(nameof(sink));
        _redactor = redactor;
    }

    public ILogger CreateLogger(string categoryName) =>
        new SinkLogger(categoryName, _sink, _redactor);

    public void Dispose()
    {
    }

    private sealed class SinkLogger : ILogger
    {
        private readonly string _category;
        private readonly IAppLogSink _sink;
        private readonly ISensitiveDataRedactor? _redactor;

        public SinkLogger(string category, IAppLogSink sink, ISensitiveDataRedactor? redactor)
        {
            _category = category;
            _sink = sink;
            _redactor = redactor;
        }

        public IDisposable? BeginScope<TState>(TState state) where TState : notnull => null;

        public bool IsEnabled(LogLevel logLevel) => logLevel != LogLevel.None;

        public void Log<TState>(
            LogLevel logLevel,
            EventId eventId,
            TState state,
            Exception? exception,
            Func<TState, Exception?, string> formatter)
        {
            if (!IsEnabled(logLevel))
            {
                return;
            }

            var message = formatter(state, exception);
            var sanitizedMessage = _redactor != null ? _redactor.Redact(message) : message;

            _sink.Emit(new AppLogEntry(
                DateTime.Now,
                logLevel,
                _category,
                sanitizedMessage,
                eventId.Id != 0 ? eventId.Id.ToString() : null,
                exception));
        }
    }
}
