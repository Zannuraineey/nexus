using System.Text.RegularExpressions;

namespace NexusDeviceService.Infrastructure.Logging;

/// <summary>
/// High-performance compiled regex sanitization engine redacting secrets, auth tokens,
/// SIM credentials, full IMEIs/IMSIs, and unbounded protocol payloads.
/// </summary>
public sealed class SensitiveDataRedactor : ISensitiveDataRedactor
{
    // Bearer / OAuth Tokens: Bearer eyJ...
    private static readonly Regex BearerTokenRegex = new(
        @"(Bearer\s+)[A-Za-z0-9\-\._~\+/]+=*",
        RegexOptions.Compiled);

    // Key-value credentials: password=xyz, token=xyz, apiKey=xyz, secret=xyz
    private static readonly Regex KeyValueSecretRegex = new(
        @"(?i)\b(password|secret|apikey|access_token|auth_token|private_key|token)\s*([:=])\s*([^\s,;""']+)",
        RegexOptions.Compiled);

    // SIM PIN / PUK commands: +CPIN: 1234, PIN=1234, PUK=12345678
    private static readonly Regex SimCredentialsRegex = new(
        @"(?i)\b(CPIN|PIN|PUK)\s*([:=])\s*(\d{4,8})",
        RegexOptions.Compiled);

    // 14-16 Digit IMEI numbers: mask leading digits, preserve last 4 for technician reference
    private static readonly Regex ImeiRegex = new(
        @"\b(\d{10,12})(\d{4})\b",
        RegexOptions.Compiled);

    // 15 Digit IMSI numbers
    private static readonly Regex ImsiRegex = new(
        @"(?i)\b(IMSI\s*[:=]?\s*)(\d{11})(\d{4})\b",
        RegexOptions.Compiled);

    // Excessive raw hex payload packets (e.g. 64+ consecutive hex chars)
    private static readonly Regex RawPayloadHexRegex = new(
        @"\b([0-9A-Fa-f]{2}[:\s-]?){32,}\b",
        RegexOptions.Compiled);

    // Facility lock passwords: AT+CLCK="PN",0,"12345678"
    private static readonly Regex FacilityPasswordRegex = new(
        @"(?i)(AT\+CLCK\s*=\s*""[A-Z]+""\s*,\s*[0-2]\s*,\s*)""([^""]+)""",
        RegexOptions.Compiled);

    public string Redact(string input)
    {
        if (string.IsNullOrEmpty(input))
        {
            return string.Empty;
        }

        var result = input;

        // Redact Facility lock passwords
        result = FacilityPasswordRegex.Replace(result, "$1\"[REDACTED_FACILITY_PASSWORD]\"");


        // 1. Redact Bearer tokens
        result = BearerTokenRegex.Replace(result, "$1[REDACTED_TOKEN]");

        // 2. Redact key-value secrets
        result = KeyValueSecretRegex.Replace(result, "$1$2[REDACTED]");

        // 3. Redact SIM PIN / PUK
        result = SimCredentialsRegex.Replace(result, "$1$2[REDACTED_SIM_CREDENTIAL]");

        // 4. Redact IMSI
        result = ImsiRegex.Replace(result, "$1[REDACTED_IMSI_****$3]");

        // 5. Redact Full IMEIs (only if matches standalone 14-16 digits)
        result = ImeiRegex.Replace(result, match =>
        {
            var last4 = match.Groups[2].Value;
            return $"[REDACTED_IMEI_****{last4}]";
        });

        // 6. Redact raw hex dumps
        result = RawPayloadHexRegex.Replace(result, "[REDACTED_RAW_PAYLOAD]");

        return result;
    }
}
