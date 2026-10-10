using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;

namespace NexusDeviceService.Desktop.ViewModels.Pages;

public sealed record HistoryRecord(DateTime Timestamp, string DevicePort, string Operation, string Status, string Elapsed);

public sealed partial class HistoryViewModel : ObservableObject
{
    private readonly ILogger<HistoryViewModel> _logger;

    public ObservableCollection<HistoryRecord> Records { get; } = new();

    public HistoryViewModel(ILogger<HistoryViewModel> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    [RelayCommand]
    private void ClearHistory()
    {
        Records.Clear();
        _logger.LogInformation("History records cleared");
    }
}
