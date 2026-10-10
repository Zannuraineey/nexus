namespace NexusDeviceService.Infrastructure.System;

public interface ISystemEnvironmentProvider
{
    string OsDescription { get; }
    string RuntimeVersion { get; }
    string MachineName { get; }
    int ProcessorCount { get; }
    bool Is64BitOperatingSystem { get; }
}
