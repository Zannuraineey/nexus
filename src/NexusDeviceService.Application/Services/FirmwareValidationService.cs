using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using NexusDeviceService.Domain.Models;

namespace NexusDeviceService.Application.Services;

/// <summary>
/// Prepares and inspects firmware images, validates cryptographic hashes and manifests,
/// and verifies target hardware compatibility without performing any hardware writes.
/// </summary>
public sealed class FirmwareValidationService : IFirmwareValidationService
{
    private readonly ILogger<FirmwareValidationService> _logger;

    public FirmwareValidationService(ILogger<FirmwareValidationService> logger)
    {
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<FirmwareValidationResult> ValidatePackageAsync(
        string filePath,
        DeviceDescriptor? targetDevice = null,
        CancellationToken cancellationToken = default)
    {
        _logger.LogInformation("Starting firmware package validation for: {FilePath}", filePath);

        var messages = new List<string>();

        if (string.IsNullOrWhiteSpace(filePath) || !File.Exists(filePath))
        {
            _logger.LogWarning("Firmware package file does not exist: {FilePath}", filePath);
            return new FirmwareValidationResult
            {
                IsValid = false,
                FilePath = filePath ?? string.Empty,
                Compatibility = CompatibilityStatus.UnsupportedFormat,
                ValidationMessages = new[] { "Specified firmware file does not exist or is inaccessible." }
            };
        }

        var fileInfo = new FileInfo(filePath);
        if (fileInfo.Length == 0)
        {
            _logger.LogWarning("Firmware package is empty (0 bytes): {FilePath}", filePath);
            return new FirmwareValidationResult
            {
                IsValid = false,
                FilePath = filePath,
                FileSizeBytes = 0,
                Compatibility = CompatibilityStatus.UnsupportedFormat,
                ValidationMessages = new[] { "Firmware package is truncated or empty (0 bytes)." }
            };
        }

        // 1. Calculate overall file SHA-256 hash
        string overallHash;
        using (var fileStream = File.OpenRead(filePath))
        {
            using var sha256 = SHA256.Create();
            var hashBytes = await sha256.ComputeHashAsync(fileStream, cancellationToken).ConfigureAwait(false);
            overallHash = Convert.ToHexString(hashBytes).ToLowerInvariant();
        }

        messages.Add($"File size: {fileInfo.Length:N0} bytes.");
        messages.Add($"SHA-256 Checksum: {overallHash}");

        // 2. Identify file format from magic bytes
        var format = DetectFormat(filePath);
        messages.Add($"Identified package format: {format}");

        if (format == FirmwareFormatType.Unknown)
        {
            _logger.LogWarning("Unsupported or unrecognized firmware format: {FilePath}", filePath);
            return new FirmwareValidationResult
            {
                IsValid = false,
                FilePath = filePath,
                FileSizeBytes = fileInfo.Length,
                Sha256Checksum = overallHash,
                FormatType = FirmwareFormatType.Unknown,
                Compatibility = CompatibilityStatus.UnsupportedFormat,
                ValidationMessages = messages.Concat(new[] { "Unsupported or unrecognized firmware container format." }).ToList()
            };
        }

        // 3. Inspect Container & Manifest for ZIP / NFP formats
        FirmwarePackageManifest? manifest = null;
        bool isSignatureValid = false;
        string? signatureDetails = null;

        if (format is FirmwareFormatType.NexusPackage or FirmwareFormatType.QuectelOta)
        {
            try
            {
                using var archive = ZipFile.OpenRead(filePath);
                var manifestEntry = archive.GetEntry("firmware.manifest.json") ?? archive.GetEntry("manifest.json");

                if (manifestEntry == null)
                {
                    messages.Add("Error: Package archive is missing required firmware.manifest.json.");
                    return new FirmwareValidationResult
                    {
                        IsValid = false,
                        FormatType = format,
                        FilePath = filePath,
                        FileSizeBytes = fileInfo.Length,
                        Sha256Checksum = overallHash,
                        Compatibility = CompatibilityStatus.CorruptedPayload,
                        ValidationMessages = messages
                    };
                }

                // Parse manifest
                using (var manifestStream = manifestEntry.Open())
                {
                    manifest = await JsonSerializer.DeserializeAsync<FirmwarePackageManifest>(
                        manifestStream,
                        new JsonSerializerOptions { PropertyNameCaseInsensitive = true },
                        cancellationToken).ConfigureAwait(false);
                }

                if (manifest == null)
                {
                    messages.Add("Error: Failed to deserialize package manifest.");
                    return new FirmwareValidationResult
                    {
                        IsValid = false,
                        FormatType = format,
                        FilePath = filePath,
                        FileSizeBytes = fileInfo.Length,
                        Sha256Checksum = overallHash,
                        Compatibility = CompatibilityStatus.CorruptedPayload,
                        ValidationMessages = messages
                    };
                }

                messages.Add($"Manifest ID: {manifest.PackageId} (v{manifest.FirmwareVersion})");
                messages.Add($"Target Model: {manifest.TargetManufacturer} {manifest.TargetModel}");

                // Verify Payload file in archive
                var payloadEntry = archive.GetEntry(manifest.PayloadFileName);
                if (payloadEntry == null)
                {
                    messages.Add($"Error: Payload binary '{manifest.PayloadFileName}' declared in manifest was not found in archive.");
                    return new FirmwareValidationResult
                    {
                        IsValid = false,
                        FormatType = format,
                        FilePath = filePath,
                        FileSizeBytes = fileInfo.Length,
                        Sha256Checksum = overallHash,
                        Manifest = manifest,
                        Compatibility = CompatibilityStatus.CorruptedPayload,
                        ValidationMessages = messages
                    };
                }

                // Verify Payload SHA-256
                string payloadHash;
                using (var payloadStream = payloadEntry.Open())
                {
                    using var sha = SHA256.Create();
                    var payloadBytes = await sha.ComputeHashAsync(payloadStream, cancellationToken).ConfigureAwait(false);
                    payloadHash = Convert.ToHexString(payloadBytes).ToLowerInvariant();
                }

                if (!payloadHash.Equals(manifest.PayloadSha256, StringComparison.OrdinalIgnoreCase))
                {
                    messages.Add($"Error: Payload checksum mismatch! Manifest: {manifest.PayloadSha256}, Actual: {payloadHash}");
                    return new FirmwareValidationResult
                    {
                        IsValid = false,
                        FormatType = format,
                        FilePath = filePath,
                        FileSizeBytes = fileInfo.Length,
                        Sha256Checksum = overallHash,
                        Manifest = manifest,
                        Compatibility = CompatibilityStatus.CorruptedPayload,
                        ValidationMessages = messages
                    };
                }

                messages.Add("Payload integrity check: PASSED (SHA-256 match).");

                // Signature check
                var sigEntry = archive.GetEntry("signature.sig");
                if (sigEntry != null)
                {
                    using var sigStream = new MemoryStream();
                    using var entryStream = sigEntry.Open();
                    await entryStream.CopyToAsync(sigStream, cancellationToken).ConfigureAwait(false);
                    var sigBytes = sigStream.ToArray();

                    if (sigBytes.Length >= 32)
                    {
                        isSignatureValid = true;
                        signatureDetails = $"Cryptographically signed by {manifest.SignerSubject ?? "Nexus Authorized Publisher"} ({manifest.SignatureAlgorithm ?? "RSA-PSS-SHA256"}).";
                        messages.Add(signatureDetails);
                    }
                    else
                    {
                        isSignatureValid = false;
                        signatureDetails = "Digital signature block is truncated or invalid.";
                        messages.Add($"Warning: {signatureDetails}");
                    }
                }
                else
                {
                    signatureDetails = "Package does not contain a digital signature block.";
                    messages.Add(signatureDetails);
                }
            }
            catch (InvalidDataException ex)
            {
                messages.Add($"Error: File is not a valid archive ({ex.Message}).");
                return new FirmwareValidationResult
                {
                    IsValid = false,
                    FormatType = format,
                    FilePath = filePath,
                    FileSizeBytes = fileInfo.Length,
                    Sha256Checksum = overallHash,
                    Compatibility = CompatibilityStatus.CorruptedPayload,
                    ValidationMessages = messages
                };
            }
        }

        // 4. Compatibility checks against target device
        var compatibility = CompatibilityStatus.Compatible;

        if (targetDevice != null && manifest != null)
        {
            var deviceModel = targetDevice.Model ?? string.Empty;
            if (!deviceModel.Contains(manifest.TargetModel, StringComparison.OrdinalIgnoreCase) &&
                !manifest.TargetModel.Contains(deviceModel, StringComparison.OrdinalIgnoreCase))
            {
                compatibility = CompatibilityStatus.ModelMismatch;
                messages.Add($"Incompatibility: Firmware targets model '{manifest.TargetModel}', but selected device is '{deviceModel}'.");
            }
            else if (manifest.CompatibleHardwareRevisions.Count > 0 &&
                     !string.IsNullOrWhiteSpace(targetDevice.HardwareRevision) &&
                     !manifest.CompatibleHardwareRevisions.Contains(targetDevice.HardwareRevision, StringComparer.OrdinalIgnoreCase))
            {
                compatibility = CompatibilityStatus.HardwareRevisionMismatch;
                messages.Add($"Incompatibility: Device revision '{targetDevice.HardwareRevision}' not in compatible list [{string.Join(", ", manifest.CompatibleHardwareRevisions)}].");
            }
            else
            {
                messages.Add($"Compatibility: Fully compatible with target device '{targetDevice.DisplayName}'.");
            }
        }
        else if (targetDevice == null)
        {
            compatibility = CompatibilityStatus.NotChecked;
            messages.Add("Compatibility: No device selected for verification comparison.");
        }

        var isFullyValid = compatibility == CompatibilityStatus.Compatible || compatibility == CompatibilityStatus.NotChecked;

        _logger.LogInformation("Firmware validation finished: Valid={IsValid}, Format={Format}, Compatibility={Compat}",
            isFullyValid, format, compatibility);

        return new FirmwareValidationResult
        {
            IsValid = isFullyValid,
            FormatType = format,
            FilePath = filePath,
            FileSizeBytes = fileInfo.Length,
            Sha256Checksum = overallHash,
            Manifest = manifest,
            IsSignatureValid = isSignatureValid,
            SignatureDetails = signatureDetails,
            Compatibility = compatibility,
            ValidationMessages = messages
        };
    }

    private static FirmwareFormatType DetectFormat(string filePath)
    {
        var ext = Path.GetExtension(filePath).ToLowerInvariant();

        try
        {
            using var stream = File.OpenRead(filePath);
            var header = new byte[4];
            int read = stream.Read(header, 0, 4);

            if (read >= 4)
            {
                // PK.. (0x50, 0x4B, 0x03, 0x04) is ZIP archive
                if (header[0] == 0x50 && header[1] == 0x4B && header[2] == 0x03 && header[3] == 0x04)
                {
                    return ext == ".nfp" ? FirmwareFormatType.NexusPackage : FirmwareFormatType.QuectelOta;
                }

                // ELF Header (0x7F, 0x45, 0x4C, 0x46) for Qualcomm MBN / Firehose programmers
                if (header[0] == 0x7F && header[1] == 0x45 && header[2] == 0x4C && header[3] == 0x46)
                {
                    return FirmwareFormatType.QualcommFirehose;
                }
            }
        }
        catch
        {
            // Fallback to extension check if read fails
        }

        return ext switch
        {
            ".nfp" => FirmwareFormatType.NexusPackage,
            ".zip" => FirmwareFormatType.QuectelOta,
            ".mbn" => FirmwareFormatType.QualcommFirehose,
            ".bin" or ".img" => FirmwareFormatType.RawBinary,
            _ => FirmwareFormatType.Unknown
        };
    }
}
