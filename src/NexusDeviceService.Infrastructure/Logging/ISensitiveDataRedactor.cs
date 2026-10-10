namespace NexusDeviceService.Infrastructure.Logging;

/// <summary>
/// Defensive sanitization service that intercepts log messages and diagnostics
/// to scrub secrets, credentials, hardware identifiers, and raw payloads.
/// </summary>
public interface ISensitiveDataRedactor
{
    string Redact(string input);
}
