using System.Runtime.InteropServices;

namespace NexusDeviceService.Infrastructure.System;

public sealed class SystemEnvironmentProvider : ISystemEnvironmentProvider
{
    public string OsDescription => RuntimeInformation.OSDescription;
    public string RuntimeVersion => RuntimeInformation.FrameworkDescription;
    public string MachineName => Environment.MachineName;
    public int ProcessorCount => Environment.ProcessorCount;
    public bool Is64BitOperatingSystem => Environment.Is64BitOperatingSystem;
}
