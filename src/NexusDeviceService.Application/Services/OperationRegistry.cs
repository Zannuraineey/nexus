using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Thread-safe registry that validates and orchestrates registered modules and operations.
/// Enforces duplicate-ID rejection, missing dependency detection, and defensive capability gating.
/// </summary>
public sealed class OperationRegistry : IOperationRegistry
{
    private readonly ILogger<OperationRegistry> _logger;
    private readonly object _syncRoot = new();
    private readonly Dictionary<string, IDeviceModule> _modules = new(StringComparer.OrdinalIgnoreCase);

    public IReadOnlyList<IDeviceModule> RegisteredModules
    {
        get
        {
            lock (_syncRoot)
            {
                return _modules.Values.ToList();
            }
        }
    }

    public OperationRegistry(
        IEnumerable<IDeviceModule> initialModules,
        ILogger<OperationRegistry> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (initialModules != null)
        {
            foreach (var module in initialModules)
            {
                RegisterModule(module);
            }
        }

        // Run validation on initial configuration
        var issues = ValidateRegistry();
        foreach (var issue in issues)
        {
            if (issue.IsFatal)
            {
                _logger.LogError("Module validation error for [{ModuleId}]: {Message}", issue.ModuleId, issue.Message);
            }
            else
            {
                _logger.LogWarning("Module validation warning for [{ModuleId}]: {Message}", issue.ModuleId, issue.Message);
            }
        }
    }

    public void RegisterModule(IDeviceModule module)
    {
        ArgumentNullException.ThrowIfNull(module);

        if (string.IsNullOrWhiteSpace(module.ModuleId))
        {
            throw new ArgumentException("Module ID cannot be null, empty, or whitespace.", nameof(module));
        }

        lock (_syncRoot)
        {
            if (_modules.ContainsKey(module.ModuleId))
            {
                throw new InvalidOperationException(
                    $"Duplicate module registration rejected: A module with ID '{module.ModuleId}' is already registered.");
            }

            _modules[module.ModuleId] = module;
            _logger.LogInformation("Registered device module '{DisplayName}' [{ModuleId}] v{Version}",
                module.DisplayName, module.ModuleId, module.Version);
        }
    }

    public IDeviceModule? GetModule(string moduleId)
    {
        if (string.IsNullOrWhiteSpace(moduleId))
        {
            return null;
        }

        lock (_syncRoot)
        {
            return _modules.TryGetValue(moduleId, out var module) ? module : null;
        }
    }

    public IReadOnlyList<IOperationHandler> GetAvailableOperations()
    {
        lock (_syncRoot)
        {
            var operations = new List<IOperationHandler>();
            var activeIssues = ValidateRegistry().ToDictionary(i => i.ModuleId, i => i);

            foreach (var module in _modules.Values)
            {
                if (module.AvailabilityStatus != ModuleAvailabilityStatus.Available)
                {
                    continue;
                }

                // If module has fatal dependency failure, do not offer its operations
                if (activeIssues.TryGetValue(module.ModuleId, out var issue) && issue.IsFatal)
                {
                    continue;
                }

                operations.AddRange(module.Operations);
            }

            return operations;
        }
    }

    public IReadOnlyList<IOperationHandler> GetOperationsForDevice(DeviceDescriptor device)
    {
        if (device == null)
        {
            return Array.Empty<IOperationHandler>();
        }

        lock (_syncRoot)
        {
            var availableOps = GetAvailableOperations();
            var matchedOps = new List<IOperationHandler>();

            foreach (var op in availableOps)
            {
                // Capability gating: Device must satisfy all required capability flags
                var required = op.Descriptor.RequiredCapabilities;
                if (required != DeviceCapability.None &&
                    (device.SupportedCapabilities & required) != required)
                {
                    _logger.LogDebug(
                        "Gating check: Operation '{OpId}' filtered out because device [{Serial}] lacks required capabilities ({Req} vs {Dev})",
                        op.Descriptor.OperationId, device.Identifier.SerialNumber ?? "Unknown", required, device.SupportedCapabilities);
                    continue;
                }

                matchedOps.Add(op);
            }

            return matchedOps;
        }
    }

    public IOperationHandler? GetOperation(string operationId)
    {
        if (string.IsNullOrWhiteSpace(operationId))
        {
            return null;
        }

        lock (_syncRoot)
        {
            return _modules.Values
                .SelectMany(m => m.Operations)
                .FirstOrDefault(op => string.Equals(op.Descriptor.OperationId, operationId, StringComparison.OrdinalIgnoreCase));
        }
    }

    public IReadOnlyList<ModuleValidationIssue> ValidateRegistry()
    {
        lock (_syncRoot)
        {
            var issues = new List<ModuleValidationIssue>();

            foreach (var module in _modules.Values)
            {
                // Validate upstream dependencies
                foreach (var depId in module.RequiredDependencyModuleIds)
                {
                    if (!_modules.ContainsKey(depId))
                    {
                        issues.Add(new ModuleValidationIssue
                        {
                            ModuleId = module.ModuleId,
                            Message = $"Required dependency '{depId}' is not registered in the system.",
                            IsFatal = true,
                            MissingDependencyId = depId
                        });
                    }
                }

                // Validate operation IDs are unique within the module
                var opIds = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
                foreach (var op in module.Operations)
                {
                    if (!opIds.Add(op.Descriptor.OperationId))
                    {
                        issues.Add(new ModuleValidationIssue
                        {
                            ModuleId = module.ModuleId,
                            Message = $"Module contains duplicate operation ID '{op.Descriptor.OperationId}'.",
                            IsFatal = true
                        });
                    }
                }
            }

            return issues;
        }
    }
}
