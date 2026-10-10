using System.Text.Json;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Infrastructure.Storage;

/// <summary>
/// Thread-safe, asynchronous file-based preference store that persists user configuration to JSON.
/// </summary>
public sealed class FilePreferenceStore : IPreferenceStore, IThemePreferenceStore
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        WriteIndented = true,
        PropertyNameCaseInsensitive = true
    };

    private readonly string _filePath;
    private readonly ILogger<FilePreferenceStore> _logger;
    private readonly SemaphoreSlim _fileLock = new(1, 1);
    private ApplicationPreferences? _cachedPreferences;

    public FilePreferenceStore(
        ILogger<FilePreferenceStore> logger,
        string? customFilePath = null)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));

        if (!string.IsNullOrWhiteSpace(customFilePath))
        {
            _filePath = customFilePath;
        }
        else
        {
            var localAppData = Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData);
            var folder = Path.Combine(localAppData, "NexusDeviceService");
            _filePath = Path.Combine(folder, "preferences.json");
        }
    }

    public async Task<ApplicationPreferences> LoadPreferencesAsync(CancellationToken cancellationToken = default)
    {
        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            if (_cachedPreferences != null)
            {
                return _cachedPreferences;
            }

            if (!File.Exists(_filePath))
            {
                _logger.LogInformation("No preference file found at {Path}; returning default preferences.", _filePath);
                _cachedPreferences = new ApplicationPreferences();
                return _cachedPreferences;
            }

            var json = await File.ReadAllTextAsync(_filePath, cancellationToken).ConfigureAwait(false);
            var prefs = JsonSerializer.Deserialize<ApplicationPreferences>(json, JsonOptions);

            _cachedPreferences = prefs ?? new ApplicationPreferences();
            _logger.LogInformation("Loaded user preferences successfully from {Path}", _filePath);
            return _cachedPreferences;
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to load preferences from {Path}; falling back to defaults.", _filePath);
            _cachedPreferences = new ApplicationPreferences();
            return _cachedPreferences;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    public async Task SavePreferencesAsync(ApplicationPreferences preferences, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(preferences);

        await _fileLock.WaitAsync(cancellationToken).ConfigureAwait(false);
        try
        {
            var dir = Path.GetDirectoryName(_filePath);
            if (!string.IsNullOrEmpty(dir) && !Directory.Exists(dir))
            {
                Directory.CreateDirectory(dir);
            }

            var updated = preferences with { LastModified = DateTime.UtcNow };
            var json = JsonSerializer.Serialize(updated, JsonOptions);

            // Atomic file write using temporary file replacement
            var tempPath = _filePath + ".tmp";
            await File.WriteAllTextAsync(tempPath, json, cancellationToken).ConfigureAwait(false);
            File.Move(tempPath, _filePath, overwrite: true);

            _cachedPreferences = updated;
            _logger.LogInformation("Saved user preferences successfully to {Path}", _filePath);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Failed to write user preferences to {Path}", _filePath);
            throw;
        }
        finally
        {
            _fileLock.Release();
        }
    }

    // IThemePreferenceStore adapter methods
    public AppTheme GetPreferredTheme()
    {
        try
        {
            var prefs = LoadPreferencesAsync().GetAwaiter().GetResult();
            return Enum.TryParse<AppTheme>(prefs.Theme, out var theme) ? theme : AppTheme.Dark;
        }
        catch
        {
            return AppTheme.Dark;
        }
    }

    public async Task SetPreferredThemeAsync(AppTheme theme, CancellationToken cancellationToken = default)
    {
        var prefs = await LoadPreferencesAsync(cancellationToken).ConfigureAwait(false);
        await SavePreferencesAsync(prefs with { Theme = theme.ToString() }, cancellationToken).ConfigureAwait(false);
    }
}
