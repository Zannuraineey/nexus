using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Application.Services;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Infrastructure.Logging;
using NexusDeviceService.Infrastructure.Storage;
using NexusDeviceService.Infrastructure.System;

namespace NexusDeviceService.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddNexusServices(this IServiceCollection services)
    {
        // Infrastructure Core
        services.AddSingleton<IAppLogSink, InMemoryLogSink>();
        services.AddSingleton<ISystemEnvironmentProvider, SystemEnvironmentProvider>();
        services.AddSingleton<ISensitiveDataRedactor, SensitiveDataRedactor>();
        services.AddSingleton<ILogExporter, FileLogExporter>();

        // Local Storage & Persistence
        services.AddSingleton<FilePreferenceStore>();
        services.AddSingleton<IPreferenceStore>(sp => sp.GetRequiredService<FilePreferenceStore>());
        services.AddSingleton<IThemePreferenceStore>(sp => sp.GetRequiredService<FilePreferenceStore>());

        // Logging provider registration with redactor
        services.AddLogging(builder =>
        {
            builder.ClearProviders();
            builder.AddConsole();
            builder.Services.AddSingleton<ILoggerProvider, SinkLoggerProvider>();
        });

        // Application services
        services.AddSingleton<IDeviceSessionManager, DeviceSessionManager>();
        services.AddSingleton<IOperationOrchestrator, OperationOrchestrator>();
        services.AddSingleton<IDeviceDiscoveryService, DeviceDiscoveryService>();

        // Hardware Device Discovery Provider (Physical Windows Hardware)
        services.AddSingleton<IWindowsDeviceEnumerator, NexusDeviceService.Infrastructure.Windows.WindowsRegistryDeviceEnumerator>();
        services.AddSingleton<NexusDeviceService.Infrastructure.Windows.WindowsDeviceDiscoveryProvider>();
        services.AddSingleton<NexusDeviceService.DeviceAbstractions.IDeviceDiscoveryProvider>(sp =>
            sp.GetRequiredService<NexusDeviceService.Infrastructure.Windows.WindowsDeviceDiscoveryProvider>());

        // Real Hardware Dashboard Data Provider
        services.AddSingleton<NexusDeviceService.Infrastructure.System.HardwareDashboardDataProvider>();
        services.AddSingleton<IDashboardDataProvider>(sp =>
            sp.GetRequiredService<NexusDeviceService.Infrastructure.System.HardwareDashboardDataProvider>());

        // Device Modules & Operation Registry (Only Authorized Physical Hardware Modules)
        services.AddSingleton<NexusDeviceService.DeviceAbstractions.IDeviceModule, NexusDeviceService.Infrastructure.Firmware.AuthorizedFlashingModule>();
        services.AddSingleton<NexusDeviceService.DeviceAbstractions.IDeviceModule, NexusDeviceService.Infrastructure.SimUnlock.AuthorizedSimUnlockModule>();
        services.AddSingleton<NexusDeviceService.DeviceAbstractions.IOperationRegistry, OperationRegistry>();
        services.AddSingleton<IOperationExecutor, OperationExecutor>();

        // Modem Communication Channel & Transport
        services.AddTransient<IModemTransport, NexusDeviceService.Infrastructure.Modem.SerialPortModemTransport>();
        services.AddTransient<IModemCommunicationChannel, NexusDeviceService.Infrastructure.Modem.ModemCommunicationChannel>();

        // Firmware Validation Service
        services.AddSingleton<IFirmwareValidationService, FirmwareValidationService>();

        // Authorized SIM Unlock Services
        services.AddSingleton<ISimUnlockAuthorizationClient, NexusDeviceService.Infrastructure.SimUnlock.MockSimUnlockAuthorizationClient>();
        services.AddSingleton<NexusDeviceService.Infrastructure.SimUnlock.ISimUnlockAuditStore, NexusDeviceService.Infrastructure.SimUnlock.InMemorySimUnlockAuditStore>();
        services.AddSingleton<ISimUnlockService, NexusDeviceService.Infrastructure.SimUnlock.SimUnlockService>();

        // Real Device Information & Carrier Unlock Providers
        services.AddSingleton<HttpClient>();
        services.AddSingleton<IDeviceInformationProvider, NexusDeviceService.Infrastructure.Modem.ZteDeviceInformationProvider>();
        services.AddSingleton<IDeviceInformationProvider, NexusDeviceService.Infrastructure.Modem.GenericDeviceInformationProvider>();
        services.AddSingleton<IDeviceInformationService, DeviceInformationService>();

        return services;
    }
}
