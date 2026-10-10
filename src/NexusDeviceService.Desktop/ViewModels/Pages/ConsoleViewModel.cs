using System.Collections.ObjectModel;
using System.IO;
using System.Windows;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Infrastructure.Logging;

namespace NexusDeviceService.Desktop.ViewModels.Pages;

public sealed partial class ConsoleViewModel : ObservableObject, IDisposable
{
    private readonly ILogger<ConsoleViewModel> _logger;
    private readonly IAppLogSink _logSink;
    private readonly ILogExporter _logExporter;
    private readonly List<AppLogEntry> _allEntries = new();
    private readonly object _entriesLock = new();

    [ObservableProperty]
    private string _filterText = string.Empty;

    [ObservableProperty]
    private string _selectedSeverity = "All";

    [ObservableProperty]
    private string? _exportStatusMessage;

    public ObservableCollection<string> AvailableSeverities { get; } = new()
    {
        "All", "Debug", "Information", "Warning", "Error"
    };

    public ObservableCollection<AppLogEntry> FilteredLogEntries { get; } = new();

    public ConsoleViewModel(
        ILogger<ConsoleViewModel> logger,
        IAppLogSink logSink,
        ILogger<FileLogExporter>? exporterLogger = null,
        ILogExporter? logExporter = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        _logExporter = logExporter ?? new FileLogExporter(exporterLogger ?? Microsoft.Extensions.Logging.Abstractions.NullLogger<FileLogExporter>.Instance);

        // Load existing logs
        lock (_entriesLock)
        {
            _allEntries.AddRange(_logSink.GetRecentLogs());
        }

        ApplyFilters();

        // Subscribe to live logs
        _logSink.LogReceived += OnLogReceived;
    }

    private void OnLogReceived(object? sender, AppLogEntry entry)
    {
        lock (_entriesLock)
        {
            _allEntries.Add(entry);
            while (_allEntries.Count > 1000)
            {
                _allEntries.RemoveAt(0);
            }
        }

        var dispatcher = System.Windows.Application.Current?.Dispatcher;
        if (dispatcher != null && !dispatcher.HasShutdownStarted)
        {
            dispatcher.InvokeAsync(ApplyFilters);
        }
        else
        {
            ApplyFilters();
        }
    }

    partial void OnFilterTextChanged(string value) => ApplyFilters();
    partial void OnSelectedSeverityChanged(string value) => ApplyFilters();

    public void ApplyFilters()
    {
        List<AppLogEntry> snapshot;
        lock (_entriesLock)
        {
            snapshot = _allEntries.ToList();
        }

        var query = FilterText?.Trim();
        var severity = SelectedSeverity;

        var filtered = snapshot.Where(entry =>
        {
            if (severity != "All" && !string.Equals(entry.Level.ToString(), severity, StringComparison.OrdinalIgnoreCase))
            {
                return false;
            }

            if (!string.IsNullOrEmpty(query))
            {
                var matchesMessage = entry.Message.Contains(query, StringComparison.OrdinalIgnoreCase);
                var matchesCategory = entry.Category.Contains(query, StringComparison.OrdinalIgnoreCase);
                var matchesCorrelation = entry.CorrelationId != null && entry.CorrelationId.Contains(query, StringComparison.OrdinalIgnoreCase);

                if (!matchesMessage && !matchesCategory && !matchesCorrelation)
                {
                    return false;
                }
            }

            return true;
        }).TakeLast(500).ToList();

        FilteredLogEntries.Clear();
        foreach (var item in filtered)
        {
            FilteredLogEntries.Add(item);
        }
    }

    [RelayCommand]
    public void ClearConsole()
    {
        _logSink.Clear();
        lock (_entriesLock)
        {
            _allEntries.Clear();
        }
        FilteredLogEntries.Clear();
        ExportStatusMessage = "Console cleared.";
        _logger.LogInformation("Activity console cleared by user");
    }

    [RelayCommand]
    public void CopyLogs()
    {
        var text = string.Join(Environment.NewLine, FilteredLogEntries.Select(e =>
            $"[{e.Timestamp:HH:mm:ss.fff}] [{e.Level}] [{e.Category}] {(e.CorrelationId != null ? $"[{e.CorrelationId}] " : "")}{e.Message}"));

        if (!string.IsNullOrEmpty(text))
        {
            Clipboard.SetText(text);
            ExportStatusMessage = $"Copied {FilteredLogEntries.Count} log lines to clipboard.";
        }
    }

    [RelayCommand]
    public async Task ExportLogsAsync(string? formatStr)
    {
        var isJson = string.Equals(formatStr, "json", StringComparison.OrdinalIgnoreCase);
        var ext = isJson ? "json" : "log";
        var fileName = $"NexusLogs_{DateTime.Now:yyyyMMdd_HHmmss}.{ext}";

        var desktopDir = Environment.GetFolderPath(Environment.SpecialFolder.DesktopDirectory);
        var targetPath = Path.Combine(desktopDir, fileName);

        List<AppLogEntry> entriesToExport;
        lock (_entriesLock)
        {
            entriesToExport = FilteredLogEntries.ToList();
        }

        var format = isJson ? LogExportFormat.Json : LogExportFormat.Text;
        var result = await _logExporter.ExportLogsAsync(entriesToExport, targetPath, format);

        if (result.IsSuccess)
        {
            ExportStatusMessage = $"Successfully exported {result.ExportedCount} logs to {fileName}";
        }
        else
        {
            ExportStatusMessage = result.ErrorMessage;
        }
    }

    public void Dispose()
    {
        _logSink.LogReceived -= OnLogReceived;
    }
}
