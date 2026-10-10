using Microsoft.Extensions.Logging;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Logging;
using NexusDeviceService.Infrastructure.System;

namespace NexusDeviceService.Infrastructure.Simulated;

/// <summary>
/// Dedicated demonstration dashboard data provider that furnishes clearly marked simulated
/// metrics, recent operations, and activity feeds until persistent hardware telemetry is wired.
/// Prevents hard-coded values from leaking into XAML views.
/// </summary>
public sealed class DemoDashboardDataProvider : IDashboardDataProvider
{
    private readonly IDeviceDiscoveryService _discoveryService;
    private readonly ISystemEnvironmentProvider _envProvider;
    private readonly IAppLogSink _logSink;
    private readonly ILogger<DemoDashboardDataProvider> _logger;

    public bool SimulateEmptyOperations { get; set; } = false;
    public bool SimulateEmptyActivities { get; set; } = false;

    public DemoDashboardDataProvider(
        IDeviceDiscoveryService discoveryService,
        ISystemEnvironmentProvider envProvider,
        IAppLogSink logSink,
        ILogger<DemoDashboardDataProvider> logger)
    {
        _discoveryService = discoveryService ?? throw new ArgumentNullException(nameof(discoveryService));
        _envProvider = envProvider ?? throw new ArgumentNullException(nameof(envProvider));
        _logSink = logSink ?? throw new ArgumentNullException(nameof(logSink));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public Task<DashboardSnapshot> GetDashboardSnapshotAsync(CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Generating dashboard snapshot from demo data provider (Simulation Active)...");

        // 1. Device summary: pull from discovery service if populated, or generate realistic initial telemetry
        var discovered = _discoveryService.LastDiscoveredDevices;
        var deviceSummary = new DeviceConnectionSummary
        {
            TotalConnectedCount = discovered.Count > 0 ? discovered.Count : 3,
            SimulatedCount = discovered.Count > 0 ? discovered.Count(d => d.IsSimulation) : 3,
            PhysicalCount = discovered.Count > 0 ? discovered.Count(d => !d.IsSimulation) : 0,
            ActiveInterfaceOrPort = discovered.Count > 0 ? (discovered[0].Identifier.PortName ?? "USB-0") : "COM3 (Virtual)",
            PrimaryDeviceDisplayName = discovered.Count > 0 ? discovered[0].DisplayName : "Qualcomm Snapdragon X65 5G Modem"
        };

        // 2. Recent Operations (clearly labeled as simulation)
        var operations = new List<RecentOperationRecord>();
        if (!SimulateEmptyOperations)
        {
            operations.Add(new RecentOperationRecord
            {
                Id = "OP-SIM-1041",
                OperationName = "[SIMULATED] AT Diagnostic Interrogation",
                TargetDevice = "Qualcomm SDX65 (COM3)",
                Status = OperationStatus.Completed,
                Timestamp = DateTime.UtcNow.AddMinutes(-4),
                DurationText = "1.24s",
                IsSimulated = true
            });

            operations.Add(new RecentOperationRecord
            {
                Id = "OP-SIM-1040",
                OperationName = "[SIMULATED] Baseband Handshake Query",
                TargetDevice = "ZTE Blade T606 (COM5)",
                Status = OperationStatus.Completed,
                Timestamp = DateTime.UtcNow.AddMinutes(-18),
                DurationText = "480ms",
                IsSimulated = true
            });

            operations.Add(new RecentOperationRecord
            {
                Id = "OP-SIM-1039",
                OperationName = "[SIMULATED] Secure Firmware Manifest Verification",
                TargetDevice = "MediaTek Dimensity 7050 (COM9)",
                Status = OperationStatus.Completed,
                Timestamp = DateTime.UtcNow.AddMinutes(-42),
                DurationText = "2.10s",
                IsSimulated = true
            });

            operations.Add(new RecentOperationRecord
            {
                Id = "OP-SIM-1038",
                OperationName = "[SIMULATED] Carrier Lock Gating Pre-flight Check",
                TargetDevice = "Qualcomm SDX65 (COM3)",
                Status = OperationStatus.Completed,
                Timestamp = DateTime.UtcNow.AddHours(-1).AddMinutes(-12),
                DurationText = "310ms",
                IsSimulated = true
            });
        }

        // 3. Recent Activity feed
        var activities = new List<RecentActivityRecord>();
        if (!SimulateEmptyActivities)
        {
            activities.Add(new RecentActivityRecord
            {
                Id = "ACT-201",
                Timestamp = DateTime.UtcNow.AddMinutes(-2),
                Description = "Virtual simulation driver enumerated 3 mock endpoints.",
                Category = "Discovery",
                Severity = "Info"
            });

            activities.Add(new RecentActivityRecord
            {
                Id = "ACT-200",
                Timestamp = DateTime.UtcNow.AddMinutes(-12),
                Description = "Pre-flight safety gatekeeper: capability bitmask verified.",
                Category = "Safety",
                Severity = "Info"
            });

            activities.Add(new RecentActivityRecord
            {
                Id = "ACT-199",
                Timestamp = DateTime.UtcNow.AddMinutes(-35),
                Description = "Offline technician lab profile mounted successfully.",
                Category = "Session",
                Severity = "Success"
            });

            activities.Add(new RecentActivityRecord
            {
                Id = "ACT-198",
                Timestamp = DateTime.UtcNow.AddHours(-1),
                Description = "Engine initialized: Central Package Management and .NET 10 LTS verified.",
                Category = "System",
                Severity = "Info"
            });
        }

        // 4. Available device modules
        var modules = new List<DeviceModuleInfo>
        {
            new()
            {
                ModuleName = "Qualcomm Sahara/FireHose Diagnostic Module",
                Category = "Modem Protocol",
                IsReady = true,
                Description = "Diagnostic packet loader and emergency download transport interface.",
                DriverVersion = "v2.4.1 (Simulated)",
                IsSimulated = true
            },
            new()
            {
                ModuleName = "MediaTek BootROM Protocol Driver",
                Category = "Flash Loader",
                IsReady = true,
                Description = "Hardware handshake and scatter validation loader for Dimensity chipsets.",
                DriverVersion = "v3.1.0 (Simulated)",
                IsSimulated = true
            },
            new()
            {
                ModuleName = "Standard 3GPP AT Serial Command Engine",
                Category = "Serial Bus",
                IsReady = true,
                Description = "Standardized V.25ter and 3GPP 27.007 command processor with response parser.",
                DriverVersion = "v1.8.4 (Simulated)",
                IsSimulated = true
            },
            new()
            {
                ModuleName = "UNISOC / Spreadtrum Diagnostic Driver",
                Category = "Diagnostic Bus",
                IsReady = true,
                Description = "Diagnostic calibration and baseband identification interface.",
                DriverVersion = "v1.2.0 (Simulated)",
                IsSimulated = true
            },
            new()
            {
                ModuleName = "Fastboot USB Protocol Engine",
                Category = "Bootloader Bus",
                IsReady = true,
                Description = "Sparse partition and bootloader variable query adapter.",
                DriverVersion = "v2.0.0 (Simulated)",
                IsSimulated = true
            }
        };

        // 5. Application status
        var appStatus = new ApplicationStatusInfo
        {
            EngineVersion = "Nexus Engine v1.0.0-rc2 (.NET 10 LTS)",
            RuntimeFramework = $"{_envProvider.MachineName} ({_envProvider.OsDescription})",
            DatabaseStatus = "Local Storage (In-Memory Audit Tier)",
            StationProfile = "Technician Lab Station (Offline Session)",
            IsSimulationActive = true,
            ActiveLogSinkEntries = _logSink.GetRecentLogs().Count
        };

        // 6. Latest structured log events from IAppLogSink
        var rawLogs = _logSink.GetRecentLogs();
        var latestEvents = new List<LatestApplicationEvent>();

        if (rawLogs.Count > 0)
        {
            foreach (var log in rawLogs.TakeLast(6).Reverse())
            {
                latestEvents.Add(new LatestApplicationEvent
                {
                    Timestamp = log.Timestamp,
                    Level = log.Level.ToString(),
                    Source = log.Category.Split('.').LastOrDefault() ?? log.Category,
                    Message = log.Message
                });
            }
        }
        else
        {
            latestEvents.Add(new LatestApplicationEvent
            {
                Timestamp = DateTime.UtcNow,
                Level = "Information",
                Source = "EngineHost",
                Message = "Nexus platform host started; in-memory diagnostic sink initialized."
            });
        }

        var snapshot = new DashboardSnapshot
        {
            DeviceSummary = deviceSummary,
            RecentOperations = operations,
            RecentActivities = activities,
            AvailableModules = modules,
            ApplicationStatus = appStatus,
            LatestEvents = latestEvents,
            IsDemonstrationData = true,
            GeneratedAt = DateTime.UtcNow
        };

        return Task.FromResult(snapshot);
    }
}
