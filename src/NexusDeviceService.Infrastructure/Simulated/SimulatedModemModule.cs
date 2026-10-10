using Microsoft.Extensions.Logging;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Common;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Infrastructure.Simulated;

/// <summary>
/// Safe demonstration device module reporting diagnostic and modem capabilities.
/// Executes simulated operation workflows without sending real commands to physical hardware.
/// </summary>
public sealed class SimulatedModemModule : IDeviceModule
{
    private readonly List<IOperationHandler> _operations;

    public string ModuleId => "module.nexus.simulated.modem";
    public string DisplayName => "Virtual Modem Diagnostics & Protocol Engine";
    public string Version => "1.0.0-simulation";
    public DeviceCapability Capabilities =>
        DeviceCapability.InformationReading |
        DeviceCapability.ModemCommunication |
        DeviceCapability.AtCommandSession |
        DeviceCapability.SimStatusCheck |
        DeviceCapability.DiagnosticLogging;

    public IReadOnlyList<string> RequiredDependencyModuleIds { get; } = Array.Empty<string>();
    public ModuleAvailabilityStatus AvailabilityStatus => ModuleAvailabilityStatus.Available;

    public ModuleCompatibilityInfo Compatibility { get; } = new()
    {
        SupportedManufacturers = new[] { "Qualcomm", "ZTE", "MediaTek", "Virtual Simulation" },
        SupportedModels = new[] { "Snapdragon X65 5G", "Blade T606", "Dimensity 7050", "Generic Modem" },
        MinEngineVersion = "1.0.0",
        IsSimulationOnly = true,
        Notes = "Safe demonstration module for diagnostic protocol testing and UI workflows."
    };

    public IReadOnlyList<IOperationHandler> Operations => _operations;

    public SimulatedModemModule(ILogger<SimulatedModemModule> logger)
    {
        _operations = new List<IOperationHandler>
        {
            new SimulatedReadDeviceInfoHandler(logger),
            new SimulatedAtDiagnosticQueryHandler(logger),
            new SimulatedSimStatusGatingHandler(logger)
        };
    }

    private sealed class SimulatedReadDeviceInfoHandler : IOperationHandler
    {
        private readonly ILogger _logger;

        public OperationDescriptor Descriptor { get; } = new()
        {
            OperationId = "op.sim.modem.read_info",
            DisplayName = "Read Device Identification & Baseband Revision",
            Description = "Interrogates baseband registers for IMEI, chipset revision, and firmware build.",
            RequiredCapabilities = DeviceCapability.InformationReading,
            Risk = OperationRiskClassification.Safe,
            RequiresDeviceConnection = true,
            RequiresUserConfirmation = false,
            SupportsCancellation = true
        };

        public SimulatedReadDeviceInfoHandler(ILogger logger) => _logger = logger;

        public async Task<IOperationResult> ExecuteAsync(
            IOperationContext context,
            IOperationProgressReporter progressReporter)
        {
            _logger.LogInformation("Executing simulated device identification operation: {OpId}", Descriptor.OperationId);

            progressReporter.ReportProgress(10, "Initializing virtual serial port channel...", "Handshake");
            await Task.Delay(50, context.CancellationToken);

            progressReporter.ReportProgress(40, "Sending AT+CGMI / AT+CGMM query packets...", "AT Query");
            await Task.Delay(80, context.CancellationToken);

            progressReporter.ReportProgress(80, "Decoding baseband hardware identification...", "Decoding");
            await Task.Delay(50, context.CancellationToken);

            progressReporter.ReportProgress(100, "Device interrogation completed successfully.", "Finished");

            var deviceName = context.Device?.DisplayName ?? "Simulated Modem";
            return OperationResult.Success(
                $"Simulated identification read completed for {deviceName}.",
                TimeSpan.FromMilliseconds(180),
                new[] { "AT+CGMI -> Qualcomm Inc.", "AT+CGMM -> SDX65 5G Modem", "AT+CGMR -> SDX65-V1.0.2-RELEASE" });
        }
    }

    private sealed class SimulatedAtDiagnosticQueryHandler : IOperationHandler
    {
        private readonly ILogger _logger;

        public OperationDescriptor Descriptor { get; } = new()
        {
            OperationId = "op.sim.modem.at_query",
            DisplayName = "Run AT Diagnostic Smoke Sequence",
            Description = "Dispatches diagnostic AT commands to verify baseband communication responsiveness.",
            RequiredCapabilities = DeviceCapability.AtCommandSession,
            Risk = OperationRiskClassification.Diagnostic,
            RequiresDeviceConnection = true,
            RequiresUserConfirmation = false,
            SupportsCancellation = true
        };

        public SimulatedAtDiagnosticQueryHandler(ILogger logger) => _logger = logger;

        public async Task<IOperationResult> ExecuteAsync(
            IOperationContext context,
            IOperationProgressReporter progressReporter)
        {
            _logger.LogInformation("Executing simulated AT diagnostic query: {OpId}", Descriptor.OperationId);

            progressReporter.ReportProgress(20, "Transmitting AT ping...", "Ping");
            await Task.Delay(40, context.CancellationToken);

            progressReporter.ReportProgress(60, "Verifying signal quality registers (AT+CSQ)...", "CSQ");
            await Task.Delay(60, context.CancellationToken);

            progressReporter.ReportProgress(100, "Diagnostic query sequence completed.", "Finished");

            return OperationResult.Success(
                "AT diagnostic verification completed. Response code: OK (Virtual channel)",
                TimeSpan.FromMilliseconds(100),
                new[] { "TX: AT", "RX: OK", "TX: AT+CSQ", "RX: +CSQ: 31, 99" });
        }
    }

    private sealed class SimulatedSimStatusGatingHandler : IOperationHandler
    {
        private readonly ILogger _logger;

        public OperationDescriptor Descriptor { get; } = new()
        {
            OperationId = "op.sim.modem.sim_status",
            DisplayName = "Inspect SIM Lock & Carrier State",
            Description = "Reads SIM lock bitmask and carrier restriction registers without altering device state.",
            RequiredCapabilities = DeviceCapability.SimStatusCheck,
            Risk = OperationRiskClassification.Diagnostic,
            RequiresDeviceConnection = true,
            RequiresUserConfirmation = true,
            SupportsCancellation = true
        };

        public SimulatedSimStatusGatingHandler(ILogger logger) => _logger = logger;

        public async Task<IOperationResult> ExecuteAsync(
            IOperationContext context,
            IOperationProgressReporter progressReporter)
        {
            _logger.LogInformation("Executing simulated SIM status check: {OpId}", Descriptor.OperationId);

            progressReporter.ReportProgress(30, "Checking SIM slot state...", "Slot Query");
            await Task.Delay(50, context.CancellationToken);

            progressReporter.ReportProgress(70, "Reading network personalization lock flags...", "Security Query");
            await Task.Delay(50, context.CancellationToken);

            progressReporter.ReportProgress(100, "Lock state analysis complete.", "Finished");

            return OperationResult.Success(
                "SIM Lock State: Network Personalization Not Active (Unlocked / Simulation State).",
                TimeSpan.FromMilliseconds(100),
                new[] { "AT+CPIN? -> +CPIN: READY", "AT%SIMLOCK? -> %SIMLOCK: 0 (Unlocked)" });
        }
    }
}
