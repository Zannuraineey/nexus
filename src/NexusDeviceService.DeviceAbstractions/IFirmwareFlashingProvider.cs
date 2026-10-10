using NexusDeviceService.Domain.Common;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Provider contract for executing verified firmware flashing workflows 
/// on specifically authorized hardware families.
/// </summary>
public interface IFirmwareFlashingProvider
{
    Task<IOperationResult> FlashFirmwareAsync(
        DeviceDescriptor device,
        FirmwareValidationResult validatedPackage,
        IOperationProgressReporter progress,
        CancellationToken cancellationToken);
}
