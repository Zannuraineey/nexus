# Authoritative Modem Communication Specification

This specification documents the communication protocol, interface contract, and command set for the **Authorized Reference Device** in **Nexus Device Service** (Prompt 12).

---

## 1. Reference Device Profile

| Field | Authoritative Specification |
| :--- | :--- |
| **Manufacturer** | Quectel Wireless Solutions Co., Ltd. |
| **Model** | Quectel EC25-E / EG25-G LTE Cat 4 Modem |
| **Chipset Family** | Qualcomm MDM9x07 (Snapdragon X5 LTE) |
| **USB Vendor ID (VID)** | `0x2C7C` |
| **USB Product ID (PID)** | `0x0125` |
| **Operating Protocol** | 3GPP TS 27.007, 3GPP TS 27.005, and ITU-T V.25ter AT Command Set |
| **Windows Driver** | Quectel Windows USB Serial Driver (`qcusbser.sys`) / Microsoft CDC-ACM (`usbser.sys`) |
| **Interface Mapping** | `MI_02`: AT Command / Virtual Modem Port (e.g., `COM3`, `115200 8N1`, No Flow Control) |

---

## 2. Standard 3GPP Read-Only Command Set

The provider restricts interaction to non-destructive, read-only diagnostic telemetry queries.

| Command | Standard | Description | Expected Output |
| :--- | :--- | :--- | :--- |
| `AT` | V.25ter | Attention / Ping / Synchronize Baud Rate | `OK` |
| `ATI` | V.25ter | Product Identification Information | `Quectel` <br/> `EC25` <br/> `Revision: EC25EFAR06A03M4G` <br/> `OK` |
| `AT+GMR` | 3GPP TS 27.007 | Request Firmware Revision | `EC25EFAR06A03M4G` <br/> `OK` |
| `AT+GSN` | 3GPP TS 27.007 | Request Product Serial Number (IMEI) | `867890041234567` <br/> *(Redacted in application logs)* <br/> `OK` |
| `AT+CSQ` | 3GPP TS 27.007 | Signal Quality Indicator | `+CSQ: <rssi>,<ber>` <br/> e.g., `+CSQ: 22,99` <br/> `OK` |
| `AT+CPIN?` | 3GPP TS 27.007 | Enter / Query PIN Status | `+CPIN: READY` / `+CPIN: SIM PIN` <br/> `OK` |
| `AT+COPS?` | 3GPP TS 27.007 | Query Registered Network Operator | `+COPS: 0,0,"Vodafone",7` <br/> `OK` |

---

## 3. Communication Channel Invariants

1. **Serialized Access**:
   All commands are queued and executed sequentially through a `SemaphoreSlim(1, 1)` mutex. Simultaneous overlapping write operations on the same COM port are strictly prohibited.
2. **Timeout & Cooperative Cancellation**:
   Each transmission specifies an explicit timeout (default: 3000 ms, maximum: 10000 ms) and respects `CancellationToken`. If a timeout expires, the command is aborted and logged as `TimedOut`.
3. **No Silent Fallback**:
   If an interface returns `ERROR`, parity errors, or fails to respond, the provider reports `ModemCommunicationException` with clear error details. It never silently attempts commands on unrelated serial ports.
4. **Sensitive Data Redaction**:
   All transmitted and received AT strings passing through the logger are sanitized by `ISensitiveDataRedactor` before being written to in-memory sinks or disk files.
