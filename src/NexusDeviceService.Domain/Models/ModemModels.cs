namespace NexusDeviceService.Domain.Models;

public enum SimPinState
{
    Unknown,
    Ready,
    PinRequired,
    PukRequired,
    NotInserted,
    Failure
}

public sealed record ModemIdentityInfo(
    string Manufacturer,
    string Model,
    string FirmwareVersion,
    string Imei,
    string RawOutput
);

public sealed record ModemSignalInfo(
    int Rssi,
    int? Dbm,
    int BitErrorRate,
    string QualityDescription
);

public sealed record ModemOperatorInfo(
    int Mode,
    int Format,
    string OperatorName,
    int? AccessTechnology
);
