# Real Hardware Device Discovery Guide

This document describes how physical Windows device discovery is implemented in **Nexus Device Service** (Prompt 11), how physical USB devices are separated from logical interfaces, and how to manually verify detected hardware against **Windows Device Manager**.

---

## 1. Discovery Architecture

### Design Principles
1. **Targeted Platform:** Windows PnP and kernel device map integration via `IWindowsDeviceEnumerator`.
2. **Physical vs. Logical Interface Separation:**
   - A physical device (e.g. Qualcomm Snapdragon modem or USB serial composite device) often exposes multiple interfaces (`MI_00`, `MI_01`, `MI_02`).
   - `WindowsDeviceDiscoveryProvider` groups interfaces sharing a Windows `ContainerID` or parent USB instance ID under a single physical [`DeviceDescriptor`](file:///src/NexusDeviceService.Domain/Models/DeviceDescriptor.cs).
   - Each endpoint/port is captured as a [`DeviceInterfaceDescriptor`](file:///src/NexusDeviceService.Domain/Models/DeviceInterfaceDescriptor.cs) in `device.Interfaces`.
3. **Modem Verification Discipline:**
   - A device is **never** flagged as a verified modem (`IsModemVerified = false`) based solely on friendly strings like "USB Modem" in Windows Device Manager.
   - Initial detection is strictly **read-only** (`SupportedCapabilities = DeviceCapability.None`).
4. **Safety & Security:**
   - Discovery performs **zero writes** to device registers, COM ports, or firmware.
   - Sensitive physical instance serial numbers are redacted via `ISensitiveDataRedactor`.

---

## 2. Manual Verification in Windows Device Manager

To verify real hardware discovery against the Windows operating system:

### Step 1: Open Windows Device Manager
- Press <kbd>Win</kbd> + <kbd>X</kbd> and select **Device Manager** (or run `devmgmt.msc`).

### Step 2: Inspect Ports (COM & LPT)
1. Expand the **Ports (COM & LPT)** category.
2. Locate the connected serial or diagnostic device (e.g., `Qualcomm HS-USB Diagnostics 9001 (COM3)` or `USB-SERIAL CH340 (COM7)`).
3. Right-click the device and select **Properties** → **Details** tab.
4. In the **Property** dropdown:
   - Select **Hardware Ids**: Observe the `VID_xxxx&PID_yyyy` and `&MI_xx` interface identifier.
   - Select **Container ID**: Notice the GUID assigned by Windows to group composite interfaces.
   - Select **Port Name**: Verify the assigned `COMx` port matches what Nexus Device Service reports.

### Step 3: Inspect Universal Serial Bus Controllers
1. Expand **Universal Serial Bus controllers** or **Universal Serial Bus devices**.
2. Locate the parent composite device (e.g., `USB Composite Device`).
3. In **Properties** → **Details**:
   - Check **Parent**: Notice the root USB hub path.
   - Compare with the grouping key reported in Nexus Device Service logs.

### Step 4: Verify in Nexus Device Service
1. Launch Nexus Device Service (`dotnet run --project src/NexusDeviceService.Desktop`).
2. Navigate to the **Devices** page.
3. Click **Scan Devices**.
4. Both virtual simulated modems and physical Windows USB/COM devices will appear in the device inventory.
5. Notice that real physical devices display their exact port name, VID/PID, and interface breakdown while remaining safe and read-only.

---

## 3. Troubleshooting Real Device Enumeration

| Issue in Device Manager | Cause | Nexus Device Service Behavior |
| :--- | :--- | :--- |
| **Yellow Bang (Code 28)** | Missing device driver | Enumerated as generic USB node; port name will be null until driver is installed. |
| **Code 10 (Cannot start)** | Port conflict or faulty USB cable | Filtered as non-operational interface; will not cause scanner crash. |
| **Access Denied (Registry)** | Restricted security policy | Handled gracefully with warning in Activity Console log. |
| **Device Disconnected During Scan** | Device unplugged mid-enumeration | Safe `try/catch` per instance key ignores stale nodes. |
