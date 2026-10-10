# Authorized Firmware Flashing Compatibility Specification

This specification documents the authorized hardware models, firmware validation rules, flashing protocol stages, safety guardrails, and emergency recovery requirements for **Nexus Device Service** (Prompt 14).

---

## 1. Authorized Device Family & Hardware Compatibility

| Parameter | Authoritative Requirement |
| :--- | :--- |
| **Manufacturer** | Quectel Wireless Solutions Co., Ltd. |
| **Module Family** | Quectel EC25 Series (LTE Cat 4) |
| **Exact Supported Models** | `EC25-E`, `EC25-A`, `EC25-V`, `EG25-G` |
| **Hardware Revisions** | `R01`, `R02`, `R03` |
| **Required Windows Drivers** | Quectel USB Serial Driver (`qcusbser.sys`) / Diagnostic Port Driver |
| **Communication Endpoint** | High-speed USB Diagnostic Port (`MI_00`) / Emergency Download Interface |

---

## 2. Pre-Flashing Prerequisites & Guardrails

Before any flashing workflow can be initiated, the following strict pre-flight conditions must be met:

1. **Exact Package and Hardware Match**:
   - Firmware package manifest `TargetModel` must match the connected device model.
   - Target device `HardwareRevision` must be explicitly listed in `CompatibleHardwareRevisions`.
2. **Cryptographic Integrity & Signature**:
   - Package must be validated by `IFirmwareValidationService`.
   - File checksum and payload SHA-256 must match manifest values.
3. **Power & Battery Health Checks**:
   - External power supply connected or battery level >= 50%.
4. **Per-Device Mutual Exclusion**:
   - Enforced by `OperationExecutor` semaphore; no concurrent operations permitted on the target device.
5. **Explicit Destructive Confirmation**:
   - Requires explicit user confirmation acknowledging that firmware will be updated.

---

## 3. Protocol Stages & Cancellation Safety Policy

```
[Stage 1: Pre-flight Verification] (Cancellable)
       │
       ▼
[Stage 2: Device State Lock & NVRAM Backup] (Cancellable)
       │
       ▼
[Stage 3: Irreversible Partition Write] (NON-CANCELLABLE)
  ⚠️ Cancellation is strictly blocked to prevent physical bricking
       │
       ▼
[Stage 4: CRC32 / SHA-256 Verification & Device Reboot]
```

### Safety Rule on Cancellation
> [!CAUTION]
> **Irreversible Write Protection**  
> During **Stage 3 (Irreversible Partition Write)**, cancellation requests are intentionally rejected. Aborting a flash operation mid-block corrupts partition tables and renders modem baseband hardware unbootable.

---

## 4. Emergency Recovery Procedures

In the event of unexpected power loss, cable disconnection, or partial write failure:

1. **Diagnostic Failure Reporting**:
   - The engine transitions to `RequiresAttention` or `Failed` and records the exact last successful block offset in the diagnostic audit log.
2. **EDL (Emergency Download Mode) Recovery**:
   - Disconnect USB cable and remove main power.
   - Short the `FASTBOOT` / `USB_BOOT` testpoint pins on the Quectel module to ground.
   - Reconnect USB cable to force Qualcomm Emergency Download (9008) mode.
   - Use the official emergency programmer package (`prog_emmc_firehose_9x07.mbn`) to re-flash the base partitions.
