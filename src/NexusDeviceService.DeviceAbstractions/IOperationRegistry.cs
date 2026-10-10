using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.DeviceAbstractions;

/// <summary>
/// Central registry discovering, validating, and managing registered device modules and operations.
/// </summary>
public interface IOperationRegistry
{
    /// <summary>
    /// All validated modules currently registered in the platform.
    /// </summary>
    IReadOnlyList<IDeviceModule> RegisteredModules { get; }

    /// <summary>
    /// Explicitly registers a device module. Throws if a duplicate module ID is detected.
    /// </summary>
    void RegisterModule(IDeviceModule module);

    /// <summary>
    /// Finds a registered module by its unique identifier.
    /// </summary>
    IDeviceModule? GetModule(string moduleId);

    /// <summary>
    /// Returns all operation handlers across all registered, available modules.
    /// </summary>
    IReadOnlyList<IOperationHandler> GetAvailableOperations();

    /// <summary>
    /// Returns only operations whose required capabilities are supported by the specified device,
    /// preventing incompatible or unsupported operations from being offered.
    /// </summary>
    IReadOnlyList<IOperationHandler> GetOperationsForDevice(DeviceDescriptor device);

    /// <summary>
    /// Finds an operation handler by its unique operation ID.
    /// </summary>
    IOperationHandler? GetOperation(string operationId);

    /// <summary>
    /// Validates all registered modules for dependency satisfaction, duplicate IDs, and compatibility issues.
    /// </summary>
    IReadOnlyList<ModuleValidationIssue> ValidateRegistry();
}
