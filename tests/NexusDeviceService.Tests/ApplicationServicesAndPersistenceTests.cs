using System.IO;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Desktop.Services;
using NexusDeviceService.Desktop.ViewModels.Pages;
using NexusDeviceService.Domain.Models;
using NexusDeviceService.Infrastructure.Logging;
using NexusDeviceService.Infrastructure.Storage;
using Xunit;

namespace NexusDeviceService.Tests;

public class ApplicationServicesAndPersistenceTests
{
    [Fact]
    public async Task FilePreferenceStore_PersistsAndSurvivesReload()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"nexus_prefs_test_{Guid.NewGuid():N}.json");
        try
        {
            var store = new FilePreferenceStore(NullLogger<FilePreferenceStore>.Instance, tempFile);

            var initial = await store.LoadPreferencesAsync();
            Assert.Equal("Dark", initial.Theme);
            Assert.Equal(7, initial.LogRetentionDays);

            var updated = initial with
            {
                Theme = "Light",
                LanguageCode = "es-ES",
                LogRetentionDays = 14,
                MinimumLogLevel = "Debug",
                DefaultPage = "Operations",
                RequireConfirmationForDestructiveOperations = false
            };

            await store.SavePreferencesAsync(updated);
            Assert.True(File.Exists(tempFile));

            // Reload with fresh store instance to verify persistence
            var freshStore = new FilePreferenceStore(NullLogger<FilePreferenceStore>.Instance, tempFile);
            var reloaded = await freshStore.LoadPreferencesAsync();

            Assert.Equal("Light", reloaded.Theme);
            Assert.Equal("es-ES", reloaded.LanguageCode);
            Assert.Equal(14, reloaded.LogRetentionDays);
            Assert.Equal("Debug", reloaded.MinimumLogLevel);
            Assert.Equal("Operations", reloaded.DefaultPage);
            Assert.False(reloaded.RequireConfirmationForDestructiveOperations);
        }
        finally
        {
            if (File.Exists(tempFile))
            {
                File.Delete(tempFile);
            }
        }
    }

    [Fact]
    public void SensitiveDataRedactor_RedactsTokens_Passwords_PinPuk_AndIdentifiers()
    {
        var redactor = new SensitiveDataRedactor();

        // 1. Bearer Token
        var authLog = "Connecting with header Authorization: Bearer eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9.xyz";
        var scrubbedAuth = redactor.Redact(authLog);
        Assert.Contains("Bearer [REDACTED_TOKEN]", scrubbedAuth);
        Assert.DoesNotContain("eyJhbG", scrubbedAuth);

        // 2. Passwords / Secrets
        var secretLog = "Database connection: server=127.0.0.1;password=SuperSecretPassword123;apiKey=abc123xyz";
        var scrubbedSecret = redactor.Redact(secretLog);
        Assert.Contains("password=[REDACTED]", scrubbedSecret);
        Assert.Contains("apiKey=[REDACTED]", scrubbedSecret);
        Assert.DoesNotContain("SuperSecretPassword123", scrubbedSecret);

        // 3. SIM PIN / PUK
        var simLog = "Modem AT sequence: AT+CPIN: 1234 and PUK=87654321";
        var scrubbedSim = redactor.Redact(simLog);
        Assert.Contains("CPIN:[REDACTED_SIM_CREDENTIAL]", scrubbedSim);
        Assert.Contains("PUK=[REDACTED_SIM_CREDENTIAL]", scrubbedSim);
        Assert.DoesNotContain("1234", scrubbedSim);

        // 4. Full 15-digit IMEI
        var imeiLog = "Hardware query returned IMEI 864502049182345 for modem";
        var scrubbedImei = redactor.Redact(imeiLog);
        Assert.Contains("[REDACTED_IMEI_****2345]", scrubbedImei);
        Assert.DoesNotContain("86450204918", scrubbedImei);
    }

    [Fact]
    public async Task FileLogExporter_ExportsToTextAndJson_AndHandlesErrorsGracefully()
    {
        var exporter = new FileLogExporter(NullLogger<FileLogExporter>.Instance);
        var entries = new List<AppLogEntry>
        {
            new(DateTime.UtcNow, LogLevel.Information, "TestCategory", "Test log message 1", "CORR-101"),
            new(DateTime.UtcNow, LogLevel.Warning, "TestCategory", "Warning message 2", "CORR-102")
        };

        var tempText = Path.Combine(Path.GetTempPath(), $"nexus_export_{Guid.NewGuid():N}.log");
        var tempJson = Path.Combine(Path.GetTempPath(), $"nexus_export_{Guid.NewGuid():N}.json");

        try
        {
            // 1. Text export
            var textResult = await exporter.ExportLogsAsync(entries, tempText, LogExportFormat.Text);
            Assert.True(textResult.IsSuccess);
            Assert.Equal(2, textResult.ExportedCount);
            Assert.True(File.Exists(tempText));
            var textContent = await File.ReadAllTextAsync(tempText);
            Assert.Contains("Test log message 1", textContent);
            Assert.Contains("[CORR-101]", textContent);

            // 2. JSON export
            var jsonResult = await exporter.ExportLogsAsync(entries, tempJson, LogExportFormat.Json);
            Assert.True(jsonResult.IsSuccess);
            Assert.Equal(2, jsonResult.ExportedCount);
            Assert.True(File.Exists(tempJson));
            var jsonContent = await File.ReadAllTextAsync(tempJson);
            Assert.Contains("\"Message\": \"Warning message 2\"", jsonContent);

            // 3. Error handling: invalid path (e.g. invalid characters)
            var errorResult = await exporter.ExportLogsAsync(entries, "Z:\\nonexistent_drive_999\\dir\\invalid.log");
            Assert.False(errorResult.IsSuccess);
            Assert.NotNull(errorResult.ErrorMessage);
        }
        finally
        {
            if (File.Exists(tempText)) File.Delete(tempText);
            if (File.Exists(tempJson)) File.Delete(tempJson);
        }
    }

    [Fact]
    public void ConsoleViewModel_FiltersLogsBySearchQueryAndSeverity()
    {
        var sink = new InMemoryLogSink();
        sink.Emit(new AppLogEntry(DateTime.UtcNow, LogLevel.Information, "Engine", "Normal engine event"));
        sink.Emit(new AppLogEntry(DateTime.UtcNow, LogLevel.Warning, "Modem", "Carrier signal weak warning", "OP-100"));
        sink.Emit(new AppLogEntry(DateTime.UtcNow, LogLevel.Error, "Hardware", "Serial communication fault", "OP-101"));

        var vm = new ConsoleViewModel(NullLogger<ConsoleViewModel>.Instance, sink);

        Assert.Equal(3, vm.FilteredLogEntries.Count);

        // Filter by Severity: Warning
        vm.SelectedSeverity = "Warning";
        Assert.Single(vm.FilteredLogEntries);
        Assert.Equal("Carrier signal weak warning", vm.FilteredLogEntries[0].Message);

        // Filter by Search Query: "fault"
        vm.SelectedSeverity = "All";
        vm.FilterText = "fault";
        Assert.Single(vm.FilteredLogEntries);
        Assert.Equal(LogLevel.Error, vm.FilteredLogEntries[0].Level);

        // Filter by Correlation ID: "OP-100"
        vm.FilterText = "OP-100";
        Assert.Single(vm.FilteredLogEntries);
        Assert.Equal("OP-100", vm.FilteredLogEntries[0].CorrelationId);
    }

    [Fact]
    public async Task SettingsViewModel_LoadsAndSavesPreferences()
    {
        var tempFile = Path.Combine(Path.GetTempPath(), $"nexus_settings_vm_test_{Guid.NewGuid():N}.json");
        try
        {
            var store = new FilePreferenceStore(NullLogger<FilePreferenceStore>.Instance, tempFile);
            var themeService = new ThemeService(NullLogger<ThemeService>.Instance, store);

            var vm = new SettingsViewModel(NullLogger<SettingsViewModel>.Instance, themeService, store);
            await vm.LoadPreferencesAsync();

            Assert.Equal("Dark", vm.SelectedTheme);

            vm.SelectedTheme = "Light";
            vm.SelectedLanguage = "Spanish (es-ES)";
            vm.LogRetentionDays = 14;
            vm.SelectedLogLevel = "Debug";

            await vm.SaveSettingsAsync();

            // Verify stored values in store
            var saved = await store.LoadPreferencesAsync();
            Assert.Equal("Light", saved.Theme);
            Assert.Equal("es-ES", saved.LanguageCode);
            Assert.Equal(14, saved.LogRetentionDays);
            Assert.Equal("Debug", saved.MinimumLogLevel);
        }
        finally
        {
            if (File.Exists(tempFile)) File.Delete(tempFile);
        }
    }
}
