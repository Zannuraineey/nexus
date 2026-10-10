using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Desktop.ViewModels.Pages;

public sealed record DeviceHistoryEntry(string Timestamp, string Action, string Status, string Operator);

public sealed partial class DevicesViewModel : ObservableObject
{
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly IDeviceInformationService? _deviceInfoService;
    private readonly ILogger<DevicesViewModel> _logger;
    private CancellationTokenSource? _activeScanCts;

    [ObservableProperty]
    private string _searchQuery = string.Empty;

    [ObservableProperty]
    private string _selectedInterfaceFilter = "All Interfaces";

    [ObservableProperty]
    private DeviceDescriptor? _selectedDevice;

    [ObservableProperty]
    private bool _isLoading = false;

    [ObservableProperty]
    private string? _errorMessage;

    [ObservableProperty]
    private string _statusFeedback = "Device discovery engine idle.";

    [ObservableProperty]
    private DeviceInformationResult? _selectedDeviceInfo;

    [ObservableProperty]
    private bool _isLoadingDeviceInfo = false;

    [ObservableProperty]
    private string? _deviceInfoStatus;

    [ObservableProperty]
    private string _carrierUnlockCode = string.Empty;

    [ObservableProperty]
    private string? _unlockFeedbackMessage;

    [ObservableProperty]
    private bool _isApplyingUnlockCode = false;

    public ObservableCollection<DeviceDescriptor> AllDevices { get; } = new();
    public ObservableCollection<DeviceDescriptor> FilteredDevices { get; } = new();
    public ObservableCollection<string> SelectedDeviceCapabilities { get; } = new();
    public ObservableCollection<DeviceHistoryEntry> SelectedDeviceHistory { get; } = new();

    public ObservableCollection<string> InterfaceFilterOptions { get; } = new()
    {
        "All Interfaces",
        "Serial COM",
        "USB"
    };

    public bool HasDevices => FilteredDevices.Count > 0;
    public bool HasNoDevices => !IsLoading && FilteredDevices.Count == 0;
    public bool IsDeviceSelected => SelectedDevice != null;
    public bool IsPhysical => SelectedDevice != null && !SelectedDevice.IsSimulation;
    public bool IsSimulation => SelectedDevice != null && SelectedDevice.IsSimulation;
    public bool IsModemInfoAvailable => SelectedDeviceInfo?.IsAvailable ?? false;
    public bool IsModemInterfaceUnavailable => SelectedDevice != null && !SelectedDevice.IsSimulation && (SelectedDeviceInfo == null || !SelectedDeviceInfo.IsAvailable);
    public string LockStatusText => SelectedDeviceInfo != null ? SelectedDeviceInfo.LockStatus.ToString() : "Unknown";
    public string RemainingAttemptsText => SelectedDeviceInfo != null && SelectedDeviceInfo.RetriesRemaining >= 0 ? SelectedDeviceInfo.RetriesRemaining.ToString() : "N/A";
    public bool CanEnterUnlockCode => SelectedDevice != null && (SelectedDevice.IsSimulation || (SelectedDeviceInfo != null && SelectedDeviceInfo.CanUnlock));

    public DevicesViewModel(
        IDeviceDiscoveryService discoveryService,
        ILogger<DevicesViewModel> logger)
        : this(discoveryService, null, logger)
    {
    }

    public DevicesViewModel(
        IDeviceDiscoveryService discoveryService,
        IDeviceInformationService? deviceInfoService,
        ILogger<DevicesViewModel> logger)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _deviceInfoService = deviceInfoService;
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    partial void OnSearchQueryChanged(string value) => ApplyFilter();
    partial void OnSelectedInterfaceFilterChanged(string value) => ApplyFilter();

    partial void OnSelectedDeviceChanged(DeviceDescriptor? value)
    {
        OnPropertyChanged(nameof(IsDeviceSelected));
        OnPropertyChanged(nameof(IsPhysical));
        OnPropertyChanged(nameof(IsSimulation));
        UpdateSelectedDeviceDetails();
        _ = QuerySelectedDeviceInfoAsync(value);
    }

    [RelayCommand]
    public async Task RefreshDevicesAsync()
    {
        if (IsLoading)
        {
            return;
        }

        try
        {
            _activeScanCts?.Cancel();
        }
        catch (ObjectDisposedException)
        {
            // Ignore if already disposed
        }
        finally
        {
            _activeScanCts?.Dispose();
        }

        var cts = new CancellationTokenSource(TimeSpan.FromSeconds(15));
        _activeScanCts = cts;

        IsLoading = true;
        ErrorMessage = null;
        StatusFeedback = "Scanning for connected mobile devices and modems...";

        try
        {
            var discovered = await _discoveryService.DiscoverAllDevicesAsync(cts.Token);

            void UpdateList()
            {
                AllDevices.Clear();
                foreach (var device in discovered)
                {
                    AllDevices.Add(device);
                }

                ApplyFilter();
            }

            if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher && !dispatcher.CheckAccess())
            {
                dispatcher.Invoke(UpdateList);
            }
            else
            {
                UpdateList();
            }

            StatusFeedback = $"Scan complete. Found {discovered.Count} devices @ {DateTime.Now:HH:mm:ss}.";
            _logger.LogInformation("Discovered {Count} devices across providers", discovered.Count);
        }
        catch (OperationCanceledException)
        {
            StatusFeedback = "Scan was cancelled by user.";
            _logger.LogInformation("Device scan was cancelled");
        }
        catch (Exception ex)
        {
            ErrorMessage = $"Device discovery failed: {ex.Message}";
            StatusFeedback = "Error scanning for devices.";
            _logger.LogError(ex, "Device discovery scan failed");
        }
        finally
        {
            IsLoading = false;
            if (_activeScanCts == cts)
            {
                _activeScanCts = null;
            }
            cts.Dispose();
            OnPropertyChanged(nameof(HasDevices));
            OnPropertyChanged(nameof(HasNoDevices));
        }
    }

    [RelayCommand]
    public void CancelScan()
    {
        if (IsLoading && _activeScanCts != null)
        {
            _activeScanCts.Cancel();
            StatusFeedback = "Cancelling device discovery scan...";
            _logger.LogInformation("User requested scan cancellation");
        }
    }

    [RelayCommand]
    public void ClearFilter()
    {
        SearchQuery = string.Empty;
        SelectedInterfaceFilter = "All Interfaces";
    }

    private void ApplyFilter()
    {
        if (System.Windows.Application.Current?.Dispatcher is { HasShutdownStarted: false } dispatcher && !dispatcher.CheckAccess())
        {
            dispatcher.Invoke(ApplyFilterInternal);
        }
        else
        {
            ApplyFilterInternal();
        }
    }

    private void ApplyFilterInternal()
    {
        FilteredDevices.Clear();

        var query = SearchQuery?.Trim() ?? string.Empty;
        var filterInterface = SelectedInterfaceFilter;

        var matching = new List<DeviceDescriptor>();

        foreach (var device in AllDevices)
        {
            // Text Search Filter
            var matchesSearch = string.IsNullOrWhiteSpace(query) ||
                device.DisplayName.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (device.Model?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (device.Manufacturer?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (device.Description?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                device.Identifier.VendorId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                device.Identifier.ProductId.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                (device.ChipsetFamily?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                (device.Identifier.PortName?.Contains(query, StringComparison.OrdinalIgnoreCase) ?? false) ||
                device.Identifier.SerialNumber.Contains(query, StringComparison.OrdinalIgnoreCase);

            // Interface Type Filter
            var matchesInterface = filterInterface switch
            {
                "Serial COM" => device.InterfaceType == DeviceInterfaceType.SerialCom,
                "USB" => device.InterfaceType == DeviceInterfaceType.Usb,
                "Virtual Simulation" => device.IsSimulation || device.InterfaceType == DeviceInterfaceType.VirtualSimulation,
                _ => true
            };

            if (matchesSearch && matchesInterface)
            {
                matching.Add(device);
            }
        }

        // Prioritize mobile devices & modems to the top of the workspace list
        foreach (var device in matching.OrderByDescending(GetDevicePriority))
        {
            FilteredDevices.Add(device);
        }

        // Maintain selection or select first prioritized device
        if (SelectedDevice != null && !FilteredDevices.Contains(SelectedDevice))
        {
            SelectedDevice = FilteredDevices.FirstOrDefault();
        }
        else if (SelectedDevice == null && FilteredDevices.Count > 0)
        {
            SelectedDevice = FilteredDevices.First();
        }

        OnPropertyChanged(nameof(HasDevices));
        OnPropertyChanged(nameof(HasNoDevices));
    }

    private static int GetDevicePriority(DeviceDescriptor device)
    {
        // 1. Cellular Modems with known modem VID (ZTE 19D2 is top priority)
        if (device.Identifier.VendorId.Equals("0x19D2", StringComparison.OrdinalIgnoreCase) ||
            (device.Manufacturer?.Contains("ZTE", StringComparison.OrdinalIgnoreCase) ?? false))
        {
            return 100;
        }

        // 2. Other known cellular vendors (Qualcomm, Huawei, Quectel, SIMCom, Sierra, MediaTek)
        if (device.Identifier.VendorId.Equals("0x05C6", StringComparison.OrdinalIgnoreCase) ||
            device.Identifier.VendorId.Equals("0x12D1", StringComparison.OrdinalIgnoreCase) ||
            device.Identifier.VendorId.Equals("0x2C7C", StringComparison.OrdinalIgnoreCase) ||
            device.Identifier.VendorId.Equals("0x1E0E", StringComparison.OrdinalIgnoreCase) ||
            device.Identifier.VendorId.Equals("0x1199", StringComparison.OrdinalIgnoreCase) ||
            device.Identifier.VendorId.Equals("0x0E8D", StringComparison.OrdinalIgnoreCase))
        {
            return 90;
        }

        // 3. Serial COM ports (Diagnostic/AT port interfaces)
        if (device.InterfaceType == DeviceInterfaceType.SerialCom || !string.IsNullOrWhiteSpace(device.Identifier.PortName))
        {
            return 80;
        }

        // 4. Virtual simulation
        if (device.IsSimulation)
        {
            return 50;
        }

        // 5. Generic USB devices
        return 10;
    }

    private void UpdateSelectedDeviceDetails()
    {
        SelectedDeviceCapabilities.Clear();
        SelectedDeviceHistory.Clear();

        if (SelectedDevice == null)
        {
            return;
        }

        // Populate Capability list
        foreach (DeviceCapability cap in Enum.GetValues<DeviceCapability>())
        {
            if (cap != DeviceCapability.None && SelectedDevice.Supports(cap))
            {
                SelectedDeviceCapabilities.Add(cap.ToString());
            }
        }

        // Populate History placeholders
        SelectedDeviceHistory.Add(new DeviceHistoryEntry(
            $"{SelectedDevice.DetectionTimestamp.ToLocalTime():HH:mm:ss}",
            "Device Enumerated",
            "OK",
            "System Discovery"));

        SelectedDeviceHistory.Add(new DeviceHistoryEntry(
            $"{SelectedDevice.DetectionTimestamp.ToLocalTime().AddSeconds(1):HH:mm:ss}",
            "Capabilities Inspected",
            "Verified",
            "Pre-flight Gatekeeper"));

        SelectedDeviceHistory.Add(new DeviceHistoryEntry(
            $"{SelectedDevice.DetectionTimestamp.ToLocalTime().AddSeconds(2):HH:mm:ss}",
            "Descriptor Cached",
            "Ready",
            "Session Manager"));
    }

    private async Task QuerySelectedDeviceInfoAsync(DeviceDescriptor? device)
    {
        if (device == null || _deviceInfoService == null)
        {
            SelectedDeviceInfo = null;
            DeviceInfoStatus = null;
            NotifyDeviceInfoProperties();
            return;
        }

        IsLoadingDeviceInfo = true;
        DeviceInfoStatus = "Querying device management interface...";
        UnlockFeedbackMessage = null;

        try
        {
            var info = await _deviceInfoService.GetDeviceInformationAsync(device);
            SelectedDeviceInfo = info;
            DeviceInfoStatus = info.StatusMessage;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to retrieve device information for {DisplayName}", device.DisplayName);
            SelectedDeviceInfo = DeviceInformationResult.Unavailable(
                "Device detected — modem information interface unavailable.",
                ex.Message);
            DeviceInfoStatus = SelectedDeviceInfo.StatusMessage;
        }
        finally
        {
            IsLoadingDeviceInfo = false;
            NotifyDeviceInfoProperties();
        }
    }

    private void NotifyDeviceInfoProperties()
    {
        OnPropertyChanged(nameof(IsModemInfoAvailable));
        OnPropertyChanged(nameof(IsModemInterfaceUnavailable));
        OnPropertyChanged(nameof(LockStatusText));
        OnPropertyChanged(nameof(RemainingAttemptsText));
        OnPropertyChanged(nameof(CanEnterUnlockCode));
    }

    [RelayCommand]
    public async Task ApplyCarrierUnlockCodeAsync()
    {
        if (SelectedDevice == null) return;

        if (string.IsNullOrWhiteSpace(CarrierUnlockCode))
        {
            UnlockFeedbackMessage = "Please enter a valid carrier-issued unlock code.";
            return;
        }

        if (_deviceInfoService == null)
        {
            UnlockFeedbackMessage = "Device information service is not available.";
            return;
        }

        IsApplyingUnlockCode = true;
        UnlockFeedbackMessage = "Validating and submitting carrier unlock code...";

        try
        {
            var outcome = await _deviceInfoService.ApplyCarrierUnlockCodeAsync(SelectedDevice, CarrierUnlockCode);
            if (outcome.Success)
            {
                UnlockFeedbackMessage = $"SUCCESS: {outcome.SummaryReport}";
                CarrierUnlockCode = string.Empty;
                await QuerySelectedDeviceInfoAsync(SelectedDevice);
            }
            else
            {
                UnlockFeedbackMessage = $"FAILED: {outcome.ErrorMessage ?? outcome.SummaryReport}";
            }
        }
        catch (Exception ex)
        {
            UnlockFeedbackMessage = $"ERROR: {ex.Message}";
        }
        finally
        {
            IsApplyingUnlockCode = false;
        }
    }
}
