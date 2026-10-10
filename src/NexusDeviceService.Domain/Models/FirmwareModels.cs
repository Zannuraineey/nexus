namespace NexusDeviceService.Domain.Models;

public enum FirmwareFormatType
{
    Unknown,
    NexusPackage,       // Standard .nfp (Container with manifest.json, payload.bin, signature.sig)
    RawBinary,          // .bin / .img raw flash image
    QualcommFirehose,   // .mbn firehose programmer
    QuectelOta          // .zip OTA delta / full archive
}

public enum CompatibilityStatus
{
    NotChecked,
    Compatible,
    ModelMismatch,
    HardwareRevisionMismatch,
    CorruptedPayload,
    SignatureInvalid,
    UnsupportedFormat
}

public sealed record FirmwarePackageManifest
{
    public required string PackageId { get; init; }
    public required string TargetManufacturer { get; init; }
    public required string TargetModel { get; init; }
    public IReadOnlyList<string> CompatibleHardwareRevisions { get; init; } = Array.Empty<string>();
    public required string FirmwareVersion { get; init; }
    public required string PayloadFileName { get; init; }
    public required string PayloadSha256 { get; init; }
    public string? SignerSubject { get; init; }
    public string? SignatureAlgorithm { get; init; }
}

public sealed record FirmwareValidationResult
{
    public bool IsValid { get; init; }
    public FirmwareFormatType FormatType { get; init; } = FirmwareFormatType.Unknown;
    public string FilePath { get; init; } = string.Empty;
    public long FileSizeBytes { get; init; }
    public string Sha256Checksum { get; init; } = string.Empty;
    public FirmwarePackageManifest? Manifest { get; init; }
    public bool IsSignatureValid { get; init; }
    public string? SignatureDetails { get; init; }
    public CompatibilityStatus Compatibility { get; init; } = CompatibilityStatus.NotChecked;
    public IReadOnlyList<string> ValidationMessages { get; init; } = Array.Empty<string>();
}
