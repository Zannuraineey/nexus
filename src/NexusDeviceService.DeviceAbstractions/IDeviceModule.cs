using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Extensible pluggable module contract declaring device diagnostic capabilities,
/// operations, dependencies, and target device compatibility.
/// </summary>
public interface IDeviceModule
{
    /// <summary>
    /// Stable, reverse-DNS unique identifier for the module (e.g. module.nexus.modem.at).
    /// </summary>
    string ModuleId { get; }

    /// <summary>
    /// Human-friendly display name.
    /// </summary>
    string DisplayName { get; }

    /// <summary>
    /// Semantic version of the module.
    /// </summary>
    string Version { get; }

    /// <summary>
    /// Bitmask of hardware capabilities furnished by this module.
    /// </summary>
    DeviceCapability Capabilities { get; }

    /// <summary>
    /// Required upstream module IDs that must be loaded and available for this module to function.
    /// </summary>
    IReadOnlyList<string> RequiredDependencyModuleIds { get; }

    /// <summary>
    /// Current operational readiness status.
    /// </summary>
    ModuleAvailabilityStatus AvailabilityStatus { get; }

    /// <summary>
    /// Manufacturer, model, and engine compatibility declaration.
    /// </summary>
    ModuleCompatibilityInfo Compatibility { get; }

    /// <summary>
    /// Collection of executable operation handlers hosted by this module.
    /// </summary>
    IReadOnlyList<IOperationHandler> Operations { get; }
}
