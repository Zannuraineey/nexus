using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Desktop.Services;
using NexusDeviceService.Infrastructure.Theme;
using Xunit;

namespace NexusDeviceService.Tests;

public class ThemeServiceTests
{
    [Fact]
    public async Task PreferenceStore_SavesAndRetrievesTheme()
    {
        var store = new InMemoryThemePreferenceStore();

        Assert.Equal(AppTheme.Dark, store.GetPreferredTheme());

        await store.SetPreferredThemeAsync(AppTheme.Light);
        Assert.Equal(AppTheme.Light, store.GetPreferredTheme());
    }

    [Fact]
    public void ThemeService_SetTheme_FiresEvent()
    {
        var store = new InMemoryThemePreferenceStore();
        var themeService = new ThemeService(NullLogger<ThemeService>.Instance, store);

        AppTheme? receivedTheme = null;
        themeService.ThemeChanged += (s, theme) => receivedTheme = theme;

        themeService.SetTheme(AppTheme.Light);

        Assert.Equal(AppTheme.Light, themeService.CurrentTheme);
        Assert.Equal(AppTheme.Light, receivedTheme);
    }

    [Fact]
    public void ThemeService_ToggleTheme_SwitchesTheme()
    {
        var store = new InMemoryThemePreferenceStore();
        var themeService = new ThemeService(NullLogger<ThemeService>.Instance, store);

        Assert.Equal(AppTheme.Dark, themeService.CurrentTheme);

        themeService.ToggleTheme();
        Assert.Equal(AppTheme.Light, themeService.CurrentTheme);

        themeService.ToggleTheme();
        Assert.Equal(AppTheme.Dark, themeService.CurrentTheme);
    }
}
