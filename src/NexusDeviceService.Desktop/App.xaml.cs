using System.Windows;
using System.Windows.Threading;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Desktop.ViewModels;
using NexusDeviceService.Infrastructure.Extensions;

namespace NexusDeviceService.Desktop;

/// <summary>
/// Composition root and application lifecycle host for Nexus Device Service Desktop.
/// </summary>
public partial class App : System.Windows.Application
{
    public static IServiceProvider Services { get; private set; } = null!;

    public App()
    {
        var serviceCollection = new ServiceCollection();
        ConfigureServices(serviceCollection);
        Services = serviceCollection.BuildServiceProvider();

        SetupGlobalExceptionHandling();
    }

    private static void ConfigureServices(IServiceCollection services)
    {
        // Core application & infrastructure services
        services.AddNexusServices();

        // Presentation Services, ViewModels & Views
        services.AddSingleton<NexusDeviceService.Desktop.Services.IThemeService, NexusDeviceService.Desktop.Services.ThemeService>();
        services.AddSingleton<NexusDeviceService.Desktop.Services.INavigationService, NexusDeviceService.Desktop.Services.NavigationService>();

        // Page ViewModels
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.DashboardViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.DevicesViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.OperationsViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.FirmwareViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.ConsoleViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.HistoryViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.SettingsViewModel>();
        services.AddSingleton<NexusDeviceService.Desktop.ViewModels.Pages.AboutViewModel>();

        services.AddSingleton<MainViewModel>();
        services.AddSingleton<MainWindow>();
    }

    private static int _isShowingDialog = 0;
    private static bool _isInitialized = false;

    protected override void OnStartup(StartupEventArgs e)
    {
        base.OnStartup(e);

        try
        {
            var themeService = Services.GetRequiredService<NexusDeviceService.Desktop.Services.IThemeService>();
            themeService.Initialize();

            var logger = Services.GetRequiredService<ILogger<App>>();
            logger.LogInformation("Nexus Device Service application starting up with theme: {Theme}", themeService.CurrentTheme);

            var mainWindow = Services.GetRequiredService<MainWindow>();
            mainWindow.Show();

            _isInitialized = true;
        }
        catch (Exception ex)
        {
            HandleFatalStartupException(ex);
        }
    }

    private static string GetDiagnosticLogPath()
    {
        try
        {
            var dir = System.IO.Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "NexusDeviceService", "Logs");
            System.IO.Directory.CreateDirectory(dir);
            return System.IO.Path.Combine(dir, $"nexus-crash-{DateTime.UtcNow:yyyyMMdd}.log");
        }
        catch
        {
            return "nexus-crash.log";
        }
    }

    private static string FormatExceptionDetails(Exception ex)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine($"[Timestamp: {DateTime.UtcNow:O}]");
        var current = ex;
        int level = 0;
        while (current != null)
        {
            sb.AppendLine($"--- Exception Level {level} [{current.GetType().FullName}] ---");
            sb.AppendLine($"Message: {current.Message}");
            sb.AppendLine($"HResult: 0x{current.HResult:X8}");
            if (current is System.Windows.Markup.XamlParseException xamlEx)
            {
                sb.AppendLine($"XAML Position: Line {xamlEx.LineNumber}, Position {xamlEx.LinePosition}");
                if (xamlEx.BaseUri != null)
                {
                    sb.AppendLine($"BaseUri: {xamlEx.BaseUri}");
                }
            }
            if (!string.IsNullOrEmpty(current.StackTrace))
            {
                sb.AppendLine("Stack Trace:");
                sb.AppendLine(current.StackTrace);
            }
            current = current.InnerException;
            level++;
        }
        return sb.ToString();
    }

    private void HandleFatalStartupException(Exception ex)
    {
        var formatted = FormatExceptionDetails(ex);
        var logPath = GetDiagnosticLogPath();
        try
        {
            System.IO.File.AppendAllText(logPath, formatted + Environment.NewLine);
        }
        catch { }

        try
        {
            var logger = Services.GetService<ILogger<App>>();
            logger?.LogCritical(ex, "Fatal startup exception occurred.\n{Details}", formatted);
        }
        catch { }

        var innermost = ex.GetBaseException();
        var shortMessage = innermost != null && innermost != ex
            ? $"{ex.Message}\n\nRoot cause: {innermost.Message}"
            : ex.Message;

        MessageBox.Show(
            $"A fatal startup error occurred and initialization cannot continue:\n\n{shortMessage}\n\nDiagnostic log written to:\n{logPath}",
            "Nexus Device Service — Fatal Startup Error",
            MessageBoxButton.OK,
            MessageBoxImage.Error);

        Shutdown(-1);
    }

    private void SetupGlobalExceptionHandling()
    {
        DispatcherUnhandledException += (s, e) =>
        {
            var formatted = FormatExceptionDetails(e.Exception);
            var logPath = GetDiagnosticLogPath();
            try
            {
                System.IO.File.AppendAllText(logPath, formatted + Environment.NewLine);
            }
            catch { }

            try
            {
                var logger = Services.GetService<ILogger<App>>();
                logger?.LogError(e.Exception, "Unhandled UI dispatcher exception caught.\n{Details}", formatted);
            }
            catch { }

            // Prevent uncontrolled cascades of modal dialogs
            if (System.Threading.Interlocked.CompareExchange(ref _isShowingDialog, 1, 0) == 0)
            {
                try
                {
                    var innermost = e.Exception.GetBaseException();
                    var shortMessage = innermost != null && innermost != e.Exception
                        ? $"{e.Exception.Message}\n\nRoot cause: {innermost.Message}"
                        : e.Exception.Message;

                    if (!_isInitialized)
                    {
                        MessageBox.Show(
                            $"A fatal startup error occurred during UI initialization:\n\n{shortMessage}\n\nDiagnostic log written to:\n{logPath}",
                            "Nexus Device Service — Fatal Startup Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Error);

                        Shutdown(-1);
                    }
                    else
                    {
                        MessageBox.Show(
                            $"A UI error occurred:\n\n{shortMessage}\n\nDiagnostic log written to:\n{logPath}",
                            "Nexus Device Service Error",
                            MessageBoxButton.OK,
                            MessageBoxImage.Warning);
                    }
                }
                finally
                {
                    System.Threading.Interlocked.Exchange(ref _isShowingDialog, 0);
                }
            }

            e.Handled = true;
        };

        AppDomain.CurrentDomain.UnhandledException += (s, e) =>
        {
            if (e.ExceptionObject is Exception ex)
            {
                var formatted = FormatExceptionDetails(ex);
                var logPath = GetDiagnosticLogPath();
                try
                {
                    System.IO.File.AppendAllText(logPath, formatted + Environment.NewLine);
                }
                catch { }

                try
                {
                    var logger = Services.GetService<ILogger<App>>();
                    logger?.LogCritical(ex, "Fatal domain unhandled exception occurred.\n{Details}", formatted);
                }
                catch { }
            }
        };

        TaskScheduler.UnobservedTaskException += (s, e) =>
        {
            var formatted = FormatExceptionDetails(e.Exception);
            try
            {
                var logger = Services.GetService<ILogger<App>>();
                logger?.LogError(e.Exception, "Unobserved task exception caught.\n{Details}", formatted);
            }
            catch { }
            e.SetObserved();
        };
    }
}
