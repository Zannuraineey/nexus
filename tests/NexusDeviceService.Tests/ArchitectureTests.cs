using System.Reflection;
using NexusDeviceService.Application.Services;
using NexusDeviceService.DeviceAbstractions;
using NexusDeviceService.Domain.Models;
using Xunit;

namespace NexusDeviceService.Tests;

public class ArchitectureTests
{
    [Fact]
    public void Domain_MustNotReference_DesktopOrInfrastructure()
    {
        var domainAssembly = typeof(DeviceProfile).Assembly;
        var referencedAssemblies = domainAssembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(referencedAssemblies, name => name.Contains("Desktop", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedAssemblies, name => name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedAssemblies, name => name.Contains("PresentationFramework", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedAssemblies, name => name.Contains("WindowsBase", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void DeviceAbstractions_MustNotReference_DesktopOrInfrastructure()
    {
        var abstractionsAssembly = typeof(IDeviceConnection).Assembly;
        var referencedAssemblies = abstractionsAssembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(referencedAssemblies, name => name.Contains("Desktop", StringComparison.OrdinalIgnoreCase));
        Assert.DoesNotContain(referencedAssemblies, name => name.Contains("Infrastructure", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public void Application_MustNotReference_Desktop()
    {
        var applicationAssembly = typeof(OperationOrchestrator).Assembly;
        var referencedAssemblies = applicationAssembly.GetReferencedAssemblies().Select(a => a.Name!).ToList();

        Assert.DoesNotContain(referencedAssemblies, name => name.Contains("Desktop", StringComparison.OrdinalIgnoreCase));
    }
}
