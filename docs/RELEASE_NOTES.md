# Nexus Device Service - Release Notes

## Version 1.0.0 (Initial Release - Simulated Hardware Edition)

**Release Date:** October 2026  
**Build Target:** Windows 10 (Build 19041+) / Windows 11 (x64)  
**Runtime:** .NET 10 LTS Desktop Runtime  
**Status:** Verification & Staging Release (Unsigned / Test-Signed)

---

### Executive Summary

Nexus Device Service 1.0.0 is an enterprise-grade Windows desktop application built with WPF, MVVM (CommunityToolkit.Mvvm), and Clean Architecture principles. It provides diagnostic monitoring, cellular SIM tooling, and state machine-governed device operations in a 100% safe, simulated hardware execution sandbox.

> [!IMPORTANT]
> **Safety & Simulation Notice**  
> All modem drivers, flash operations, SIM credential tools, and discovery pipelines operate in **Virtual / Simulated Mode**. The service does NOT modify physical NVRAM, overwrite baseband bootloaders, or bypass carrier lockouts. Connected physical hardware remains untouched.

---

### Highlights & Key Features

#### 1. Real-time Diagnostic Dashboard
- **Fleet Metric Cards**: Visual overview of discovered devices, operational health index, active operations, and average latency.
- **Device Quick Roster**: Live status of virtual devices (Simulated Qualcomm Snapdragon X65 5G Modem, Virtual MediaTek Dimensity T750, etc.).
- **Live Activity Stream**: Streaming feed of recent operational events and diagnostic heartbeats.

#### 2. Hardware Discovery & Device Management
- **Async Device Discovery**: Background discovery pipeline scanning simulated USB, COM, and PCIe buses with cancellation support.
- **Inspection Detail Drawer**: Comprehensive telemetry including IMEI (masked), firmware revision, signal RSSI/RSRP/RSRQ, thermal sensors, and supported capabilities.
- **Device Capability Matrix**: Dynamic capability gating preventing unsupported operations from executing on selected hardware.

#### 3. Cellular & SIM Tooling Suite
- **SIM State Inspector**: Real-time IMSI, ICCID, PIN state, and operator telemetry.
- **PIN Management**: Secure PIN unlock and PIN change dialogs with failure tracking and PUK lockout simulation.
- **Carrier Profile Selector**: Profile switching with dependency validation.

#### 4. Operation Center & State Machine Lifecycle
- **Strict State Transitions**: `Created` → `Validating` → `Waiting` → `Running` → `Succeeded` / `Failed` / `Cancelled` / `RequiresAttention`.
- **Per-Device Mutual Exclusion**: Concurrency lock guarantees no simultaneous conflicting operations run on the same target device.
- **Cooperative Cancellation**: Immediate and safe abort of ongoing operations via standard `CancellationToken`.
- **Confirmation Gating**: High-impact actions (e.g., Firmware Flash, Factory Reset) mandate explicit user confirmation.

#### 5. Diagnostic Activity Console & Sensitive Data Redactor
- **Real-time Event Stream**: Timestamp, severity (`Trace`, `Debug`, `Info`, `Warning`, `Error`, `Critical`), category, message, and operation correlation IDs.
- **Sensitive Data Redaction**: Automatic compile-time regex scrubbing of Bearer tokens, secrets, PIN/PUK codes, and full 15-digit IMEIs before logging or display.
- **Multi-format Log Export**: Filtered export to human-readable plain text (`.log`) and structured JSON (`.json`) with full I/O error handling.

#### 6. User Preferences & Local Persistence
- **Theme Switching**: Instant dynamic toggling between Dark Mode and Light Mode.
- **Preference Persistence**: Atomic JSON storage in `%LocalAppData%\NexusDeviceService\preferences.json` surviving application restarts.
- **Configurable Confirmation Policies**: User preference to suppress or enforce safety confirmation prompts.

---

### System Requirements

| Component | Minimum Requirement | Recommended |
| :--- | :--- | :--- |
| **Operating System** | Windows 10 64-bit (Build 19041 or higher) | Windows 11 64-bit |
| **Architecture** | x64 (AMD64 / Intel 64) | x64 |
| **Runtime** | .NET 10.0 Desktop Runtime (x64) | .NET 10.0 Desktop Runtime (x64) |
| **Memory (RAM)** | 2 GB RAM | 4 GB+ RAM |
| **Disk Space** | 50 MB (Framework-dependent) / 250 MB (Self-contained) | 500 MB free space |
| **Display** | 1280 x 720 minimum resolution | 1920 x 1080 resolution |

---

### Known Limitations

1. **Simulated Hardware Only**: Real serial port / USB CDC ACM physical hardware communication is disabled by default; virtual device providers simulate all modem AT commands and responses.
2. **Code Signing**: Binaries in test/preview distribution are not digitally signed with a commercial EV Authenticode certificate. SmartScreen may display an unknown publisher warning during initial launch until the enterprise certificate is injected in CI/CD.

---

### Support & Feedback

For issue reports, feature requests, or diagnostic log submission:
- **Repository Issues**: `https://github.com/nexus-diagnostics/nexus-device-service/issues`
- **Support Logs**: Export logs from **Activity Console → Export Filtered Logs** or retrieve `%LocalAppData%\NexusDeviceService\Logs\`.
