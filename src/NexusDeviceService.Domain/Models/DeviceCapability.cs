namespace NexusDeviceService.Domain.Models;

/// <summary>
/// Declared capabilities supported by a device profile or service plugin.
/// Operations must explicitly verify these flags before initiating actions.
/// </summary>
[Flags]
public enum DeviceCapability
{
    None = 0,
    DeviceDiscovery = 1 << 0,
    InformationReading = 1 << 1,
    ModemCommunication = 1 << 2,
    AtCommandSession = 1 << 3,
    FirmwareValidation = 1 << 4,
    SimStatusCheck = 1 << 5,
    AuthorizedSimUnlock = 1 << 6,
    DiagnosticLogging = 1 << 7,
    FirmwareWrite = 1 << 8,
    FirmwareRead = 1 << 9
}
