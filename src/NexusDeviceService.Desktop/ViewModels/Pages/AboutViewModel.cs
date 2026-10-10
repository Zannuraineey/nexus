using CommunityToolkit.Mvvm.ComponentModel;
using NexusDeviceService.Infrastructure.System;

namespace NexusDeviceService.Desktop.ViewModels.Pages;

public sealed class AboutViewModel : ObservableObject
{
    public string AppTitle => "Nexus Device Service Desktop Platform";
    public string AppVersion => "1.0.0-preview (Phase 3 Shell Architecture)";
    public string Framework => "Microsoft .NET 10 LTS (x64 Windows Desktop)";
    public string OsInfo { get; }
    public string MachineName { get; }
    public string SafetyPolicy => "Operating in Strict Simulation Mode. No proprietary firmware binaries, credentials, or destructive operations are implemented.";

    public AboutViewModel(ISystemEnvironmentProvider envProvider)
    {
        ArgumentNullException.ThrowIfNull(envProvider);
        OsInfo = envProvider.OsDescription;
        MachineName = envProvider.MachineName;
    }
}
