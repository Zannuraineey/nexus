using System.IO;
using System.IO.Compression;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging.Abstractions;
using NexusDeviceService.Application.Services;
using NexusDeviceService.Domain.Models;
using Xunit;

namespace NexusDeviceService.Tests;

public class FirmwareValidationTests : IDisposable
{
    private readonly string _testTempDir;

    public FirmwareValidationTests()
    {
        _testTempDir = Path.Combine(Path.GetTempPath(), "Nexus_FirmwareTests_" + Guid.NewGuid().ToString("N"));
        Directory.CreateDirectory(_testTempDir);
    }

    public void Dispose()
    {
        if (Directory.Exists(_testTempDir))
        {
            try { Directory.Delete(_testTempDir, true); } catch { }
        }
    }

    private string CreateValidFirmwarePackageFixture(
        string targetManufacturer = "Quectel",
        string targetModel = "EC25",
        string[]? compatibleRevisions = null)
    {
        var packagePath = Path.Combine(_testTempDir, "Fixture_ValidPackage.nfp");

        // 1. Create payload
        var payloadBytes = Encoding.UTF8.GetBytes("FIXTURE_BINARY_FIRMWARE_PAYLOAD_V1.2.0");
        string payloadSha256;
        using (var sha = SHA256.Create())
        {
            payloadSha256 = Convert.ToHexString(sha.ComputeHash(payloadBytes)).ToLowerInvariant();
        }

        // 2. Create manifest
        var manifest = new FirmwarePackageManifest
        {
            PackageId = "PKG-QUECTEL-EC25-V120",
            TargetManufacturer = targetManufacturer,
            TargetModel = targetModel,
            CompatibleHardwareRevisions = compatibleRevisions ?? new[] { "R01", "R02" },
            FirmwareVersion = "1.2.0",
            PayloadFileName = "modem.bin",
            PayloadSha256 = payloadSha256,
            SignerSubject = "CN=Nexus Firmware Authority, O=Quectel Approved",
            SignatureAlgorithm = "RSA-PSS-SHA256"
        };

        var manifestJson = JsonSerializer.Serialize(manifest, new JsonSerializerOptions { WriteIndented = true });

        // 3. Assemble ZIP archive fixture
        using (var zipStream = new FileStream(packagePath, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            // Add manifest
            var manifestEntry = archive.CreateEntry("firmware.manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.Write(manifestJson);
            }

            // Add payload
            var payloadEntry = archive.CreateEntry("modem.bin");
            using (var pStream = payloadEntry.Open())
            {
                pStream.Write(payloadBytes);
            }

            // Add dummy signature block
            var sigEntry = archive.CreateEntry("signature.sig");
            using (var sStream = sigEntry.Open())
            {
                sStream.Write(new byte[64]); // 64 bytes valid dummy signature
            }
        }

        return packagePath;
    }

    [Fact]
    public async Task ValidatePackageAsync_ValidFixture_ReturnsSuccessAndParsesManifest()
    {
        var packagePath = CreateValidFirmwarePackageFixture();
        var service = new FirmwareValidationService(NullLogger<FirmwareValidationService>.Instance);

        var result = await service.ValidatePackageAsync(packagePath);

        Assert.True(result.IsValid);
        Assert.Equal(FirmwareFormatType.NexusPackage, result.FormatType);
        Assert.NotNull(result.Manifest);
        Assert.Equal("PKG-QUECTEL-EC25-V120", result.Manifest.PackageId);
        Assert.True(result.IsSignatureValid);
        Assert.NotEmpty(result.Sha256Checksum);
    }

    [Fact]
    public async Task ValidatePackageAsync_CorruptPayload_RejectsWithChecksumMismatch()
    {
        var packagePath = Path.Combine(_testTempDir, "Fixture_CorruptPayload.nfp");

        var manifest = new FirmwarePackageManifest
        {
            PackageId = "PKG-CORRUPT",
            TargetManufacturer = "Quectel",
            TargetModel = "EC25",
            FirmwareVersion = "1.0.0",
            PayloadFileName = "modem.bin",
            PayloadSha256 = "0000000000000000000000000000000000000000000000000000000000000000" // Mismatched hash
        };

        using (var zipStream = new FileStream(packagePath, FileMode.Create))
        using (var archive = new ZipArchive(zipStream, ZipArchiveMode.Create))
        {
            var manifestEntry = archive.CreateEntry("firmware.manifest.json");
            using (var writer = new StreamWriter(manifestEntry.Open()))
            {
                writer.Write(JsonSerializer.Serialize(manifest));
            }

            var payloadEntry = archive.CreateEntry("modem.bin");
            using (var pStream = payloadEntry.Open())
            {
                pStream.Write(Encoding.UTF8.GetBytes("ACTUAL_BYTES"));
            }
        }

        var service = new FirmwareValidationService(NullLogger<FirmwareValidationService>.Instance);
        var result = await service.ValidatePackageAsync(packagePath);

        Assert.False(result.IsValid);
        Assert.Equal(CompatibilityStatus.CorruptedPayload, result.Compatibility);
        Assert.Contains(result.ValidationMessages, m => m.Contains("mismatch", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePackageAsync_EmptyFile_RejectsWithUsefulExplanation()
    {
        var emptyPath = Path.Combine(_testTempDir, "Fixture_Empty.bin");
        File.WriteAllBytes(emptyPath, Array.Empty<byte>());

        var service = new FirmwareValidationService(NullLogger<FirmwareValidationService>.Instance);
        var result = await service.ValidatePackageAsync(emptyPath);

        Assert.False(result.IsValid);
        Assert.Equal(0, result.FileSizeBytes);
        Assert.Contains(result.ValidationMessages, m => m.Contains("empty", StringComparison.OrdinalIgnoreCase) || m.Contains("0 bytes", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePackageAsync_TargetDeviceMismatch_RejectsAsModelMismatch()
    {
        var packagePath = CreateValidFirmwarePackageFixture(targetManufacturer: "Quectel", targetModel: "EC25");
        var service = new FirmwareValidationService(NullLogger<FirmwareValidationService>.Instance);

        var incompatibleDevice = new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier { VendorId = "0x05C6", ProductId = "0x9001", SerialNumber = "DEV001" },
            Manufacturer = "MediaTek",
            Model = "Dimensity T750", // Mismatched model
            ProviderId = "test"
        };

        var result = await service.ValidatePackageAsync(packagePath, incompatibleDevice);

        Assert.False(result.IsValid);
        Assert.Equal(CompatibilityStatus.ModelMismatch, result.Compatibility);
        Assert.Contains(result.ValidationMessages, m => m.Contains("Incompatibility", StringComparison.OrdinalIgnoreCase));
    }

    [Fact]
    public async Task ValidatePackageAsync_HardwareRevisionMismatch_RejectsAsRevisionMismatch()
    {
        var packagePath = CreateValidFirmwarePackageFixture(
            targetManufacturer: "Quectel",
            targetModel: "EC25",
            compatibleRevisions: new[] { "R01", "R02" });

        var service = new FirmwareValidationService(NullLogger<FirmwareValidationService>.Instance);

        var incompatibleDevice = new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier { VendorId = "0x2C7C", ProductId = "0x0125", SerialNumber = "DEV002" },
            Manufacturer = "Quectel",
            Model = "EC25",
            HardwareRevision = "R99_UNSUPPORTED", // Revision not supported
            ProviderId = "test"
        };

        var result = await service.ValidatePackageAsync(packagePath, incompatibleDevice);

        Assert.False(result.IsValid);
        Assert.Equal(CompatibilityStatus.HardwareRevisionMismatch, result.Compatibility);
    }

    [Fact]
    public async Task ValidatePackageAsync_CompatibleDevice_PassesCompatibilityCheck()
    {
        var packagePath = CreateValidFirmwarePackageFixture(
            targetManufacturer: "Quectel",
            targetModel: "EC25",
            compatibleRevisions: new[] { "R01", "R02" });

        var service = new FirmwareValidationService(NullLogger<FirmwareValidationService>.Instance);

        var compatibleDevice = new DeviceDescriptor
        {
            Identifier = new DeviceIdentifier { VendorId = "0x2C7C", ProductId = "0x0125", SerialNumber = "DEV003" },
            Manufacturer = "Quectel",
            Model = "EC25-E LTE Modem",
            HardwareRevision = "R01",
            ProviderId = "test"
        };

        var result = await service.ValidatePackageAsync(packagePath, compatibleDevice);

        Assert.True(result.IsValid);
        Assert.Equal(CompatibilityStatus.Compatible, result.Compatibility);
    }
}
