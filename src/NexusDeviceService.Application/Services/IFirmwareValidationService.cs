using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Service responsible for validating firmware package structure, integrity hashes, 
/// digital signatures, and target device compatibility without writing to physical hardware.
/// </summary>
public interface IFirmwareValidationService
{
    Task<FirmwareValidationResult> ValidatePackageAsync(
        string filePath,
        DeviceDescriptor? targetDevice = null,
        CancellationToken cancellationToken = default);
}
