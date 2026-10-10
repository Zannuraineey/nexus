using System.Collections.ObjectModel;
using CommunityToolkit.Mvvm.ComponentModel;
using CommunityToolkit.Mvvm.Input;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Desktop.ViewModels.Pages;

public sealed partial class FirmwareViewModel : ObservableObject
{
    private readonly IFirmwareValidationService _validationService;
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly ILogger<FirmwareViewModel> _logger;

    [ObservableProperty]
    private string _imagePath = string.Empty;

    [ObservableProperty]
    private DeviceDescriptor? _selectedDevice;

    [ObservableProperty]
    private FirmwareValidationResult? _validationResult;

    [ObservableProperty]
    private bool _isLoading;

    [ObservableProperty]
    private string _statusMessage = "Select a firmware package and optionally a target device to inspect.";

    [ObservableProperty]
    private bool _isPreviewConfirmed;

    public ObservableCollection<DeviceDescriptor> AvailableDevices { get; } = new();

    public FirmwareViewModel(
        IFirmwareValidationService validationService,
        IDeviceDiscoveryService discoveryService,
        ILogger<FirmwareViewModel> logger)
    {
        _validationService = validationService ?? throw new ArgumentNullException(nameof(validationService));
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        LoadDevices();
    }

    private void LoadDevices()
    {
        AvailableDevices.Clear();
        foreach (var dev in _discoveryService.LastDiscoveredDevices)
        {
            AvailableDevices.Add(dev);
        }

        SelectedDevice = AvailableDevices.FirstOrDefault();
    }

    [RelayCommand]
    private async Task ValidatePackageAsync()
    {
        if (string.IsNullOrWhiteSpace(ImagePath))
        {
            StatusMessage = "Please enter or select a valid firmware package path.";
            return;
        }

        IsLoading = true;
        StatusMessage = "Validating cryptographic integrity, manifest structure, and device compatibility...";
        IsPreviewConfirmed = false;

        try
        {
            _logger.LogInformation("Validating firmware package '{Path}' against device '{Device}'...",
                ImagePath, SelectedDevice?.DisplayName ?? "None");

            ValidationResult = await _validationService.ValidatePackageAsync(ImagePath, SelectedDevice);

            if (ValidationResult.IsValid)
            {
                StatusMessage = "Firmware validation PASSED. File structure, payload hash, and device compatibility verified.";
            }
            else
            {
                StatusMessage = $"Firmware validation FAILED. {string.Join(" ", ValidationResult.ValidationMessages.Take(2))}";
            }
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error validating firmware package {Path}", ImagePath);
            StatusMessage = $"Validation exception: {ex.Message}";
        }
        finally
        {
            IsLoading = false;
        }
    }

    [RelayCommand]
    private void ConfirmPreview()
    {
        if (ValidationResult?.IsValid == true)
        {
            IsPreviewConfirmed = true;
            _logger.LogInformation("User confirmed preview of validated firmware package {Path}. No physical device write performed (Prompt 13 validation only).", ImagePath);
        }
    }

    [RelayCommand]
    private void Clear()
    {
        ImagePath = string.Empty;
        ValidationResult = null;
        IsPreviewConfirmed = false;
        StatusMessage = "Cleared validation state.";
    }
}
