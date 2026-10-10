# Packaging, Deployment & Verification Guide

This document defines the deployment model, repeatable release build instructions, installer configuration, testing checklists, data preservation policies, and support diagnostics for **Nexus Device Service**.

---

## 1. Runtime Deployment Model & Requirements

### Target Platform
- **Target OS:** Windows 10 (Version 2004 / Build 19041+) and Windows 11 (64-bit).
- **Target Architecture:** `win-x64`.
- **Target Framework:** `net10.0-windows` (WPF desktop application).

### Deployment Flavors
The application supports two distribution configurations:

1. **Framework-Dependent (`win-x64`) [Default / Recommended for Enterprise]**:
   - **Footprint:** ~15 MB uncompressed (~1 MB zipped).
   - **Prerequisite:** Microsoft .NET 10.0 Desktop Runtime (x64) installed on the client machine.
   - **Advantage:** Rapid updates, smaller bandwidth consumption, utilizes centrally managed Windows .NET updates.

2. **Self-Contained (`win-x64`)**:
   - **Footprint:** ~180 MB uncompressed (~60 MB zipped).
   - **Prerequisite:** None (bundles the required .NET 10 CLR and WPF libraries).
   - **Advantage:** Operates in restricted, air-gapped, or legacy environments where the .NET 10 runtime cannot be pre-installed.

---

## 2. Repeatable Release Build & Packaging Commands

### Automated Packaging (Recommended)
Execute the unified release packaging script from PowerShell:

```powershell
# Framework-dependent packaging (default)
powershell -ExecutionPolicy Bypass -File scripts\publish-release.ps1

# Self-contained packaging
powershell -ExecutionPolicy Bypass -File scripts\publish-release.ps1 -SelfContained
```

### Manual CLI Commands (Step-by-Step)

#### Step 1: Clean and Verify Automated Tests
```powershell
$env:Path = "C:\Users\lenovo\AppData\Local\Microsoft\dotnet;$env:Path"
dotnet test NexusDeviceService.slnx -c Release
```

#### Step 2: Publish Windows Binaries
```powershell
dotnet publish src\NexusDeviceService.Desktop\NexusDeviceService.Desktop.csproj `
  -c Release `
  -r win-x64 `
  --self-contained false `
  -o publish\win-x64 `
  /p:Version=1.0.0
```

#### Step 3: Compile Inno Setup Installer (Requires Inno Setup 6)
```powershell
& "C:\Program Files (x86)\Inno Setup 6\ISCC.exe" packaging\innosetup\NexusDeviceService.iss
```

#### Step 4: Generate Portable Archive (Fallback / Portable Edition)
```powershell
Compress-Archive -Path "publish\win-x64\*" -DestinationPath "dist\NexusDeviceService-1.0.0-win-x64.zip" -Force
```

---

## 3. Generated Artifact Locations

| Artifact | Path | Purpose |
| :--- | :--- | :--- |
| **Published Output Directory** | `publish\win-x64\` | Unpacked binaries, DLLs, and runtime configs |
| **Main Executable** | `publish\win-x64\NexusDeviceService.Desktop.exe` | WinExe application binary with embedded icon & version metadata |
| **Portable Distribution ZIP** | `dist\NexusDeviceService-1.0.0-win-x64.zip` | Standalone portable zip package |
| **Inno Setup Installer** | `dist\NexusDeviceService-Setup-1.0.0.exe` | Windows setup executable (when compiled via Inno Setup) |
| **Installer Configuration** | `packaging\innosetup\NexusDeviceService.iss` | Inno Setup source definition script |

---

## 4. Code Signing & Authenticode Policy

> [!WARNING]
> **Code Signing Notice**  
> Test and local development builds are **unsigned**. Do not claim these binaries or installer packages are Authenticode-signed unless a valid corporate certificate (EV code-signing USB token or Azure Key Vault HSM) has been applied.

### CI/CD Signing Integration Hook
To sign the installer and executable in an automated CI/CD pipeline, uncomment and configure the `SignTool` directive in `packaging\innosetup\NexusDeviceService.iss`:

```ini
; Example CI/CD sign tool invocation in Inno Setup:
SignTool=signtool sign /sha1 <CertThumbprint> /tr http://timestamp.digicert.com /td sha256 /fd sha256 $f
SignedUninstaller=yes
```

---

## 5. Verification Checklists

### Checklist A: Clean-Install Test
Perform this verification on a pristine Windows 10/11 machine or sandbox:

- [ ] **Installer Launch**: `NexusDeviceService-Setup-1.0.0.exe` launches without crashes.
- [ ] **SmartScreen / UAC**: Elevates cleanly or operates in user mode (`PrivilegesRequired=lowest`).
- [ ] **Destination Directory**: Defaults to `{autopf}\NexusDeviceService` (`C:\Program Files\NexusDeviceService` or `C:\Users\<user>\AppData\Local\Programs\NexusDeviceService`).
- [ ] **Shortcuts**: Start Menu shortcut and optional Desktop shortcut created with high-resolution application icon.
- [ ] **Startup**: Application launches immediately; main window opens centered at 1220x780.
- [ ] **Theme & Data**: Default Dark theme loads cleanly without missing resource dictionaries.
- [ ] **Initial Devices**: Virtual modem and peripheral devices populate the Dashboard roster.

### Checklist B: Upgrade Test (v1.0.0 → v1.0.1)
- [ ] **In-place Upgrade**: Installing an updated version over an existing installation does not throw file-in-use errors if the app is closed.
- [ ] **Settings Preservation**: User theme choice, log filter preferences, and confirmation toggle states in `%LocalAppData%\NexusDeviceService\preferences.json` are retained intact.
- [ ] **Operation History**: Existing diagnostic logs and exported reports are preserved.
- [ ] **Old File Pruning**: Superseded binary files are removed from the installation directory.

### Checklist C: Uninstall Test
- [ ] **Add/Remove Programs**: Application entry appears with correct display name ("Nexus Device Service"), publisher ("Nexus Diagnostics Lab"), and version.
- [ ] **Binary Removal**: Uninstaller completely deletes all files in the installation directory (`{app}`).
- [ ] **Shortcut Cleanup**: Start Menu and Desktop shortcuts are cleanly removed.
- [ ] **User Data Policy**: The uninstaller presents an optional confirmation prompt before removing `%LocalAppData%\NexusDeviceService`. If declined, user diagnostic logs remain intact.

---

## 6. Log and Configuration Preservation Policy

### Storage Location
User preferences, cached states, and local diagnostic files are isolated from application binaries:

```
%LocalAppData%\NexusDeviceService\
  ├── preferences.json       <-- UI theme, confirmation preferences, default page
  └── Logs\                  <-- Exported diagnostic sessions & application logs
```

### Policy Rules
1. **Never store application state in `{app}` (Program Files)**: Writing to `{app}` violates modern Windows security and requires administrative privileges.
2. **Preserve User Settings Across Upgrades**: The installer overwrites application binaries in `{app}` but never overwrites `%LocalAppData%\NexusDeviceService\preferences.json`.
3. **Sensitive Data Redaction**: Under no circumstances are bearer tokens, SIM PINs, full 15-digit IMEIs, or raw cryptographic keys written to disk. All logs are filtered through `ISensitiveDataRedactor`.

---

## 7. Crash and Support Diagnostic Guidance

When investigating user incidents or unexpected application faults, gather the following artifacts:

### 1. Application-Level Logs
- Direct the user to the **Activity Console** page, click **Export Filtered Logs**, and save a `.json` or `.log` file.
- Inspect the file for the specific **Correlation ID** associated with the failing device operation.

### 2. Windows Event Viewer (WER & Application Logs)
If the application crashes prior to UI initialization:
1. Open Windows Event Viewer (`eventvwr.msc`).
2. Navigate to **Windows Logs** → **Application**.
3. Filter by **Source**: `.NET Runtime` and `Application Error`.
4. Locate Event ID `1026` (.NET unhandled exception) or `1000` (Application crash).
5. Extract the exception stack trace and faulting module.

### 3. Windows Error Reporting (WER) Mini-Dumps
For crash analysis:
- WER mini-dumps are located at: `%LocalAppData%\CrashDumps\NexusDeviceService.Desktop.exe.*.dmp`.
- Open the `.dmp` file in Visual Studio or WinDbg to examine the thread state and call stack at the moment of failure.

---

## 8. Environment Verification Status

| Component | Status | Notes |
| :--- | :--- | :--- |
| **Release Compilation** | Verified | Built with 0 errors, 0 warnings (`dotnet build -c Release`). |
| **Automated Tests** | Verified | 58 unit and architecture tests passed in 902 ms. |
| **PE Header & Assembly Metadata** | Verified | Version `1.0.0.0`, Company `Nexus Diagnostics Lab`, Icon embedded. |
| **Framework-Dependent Publication** | Verified | Published to `publish\win-x64\` (31 clean files). |
| **Distribution ZIP Archive** | Verified | Packaged at `dist\NexusDeviceService-1.0.0-win-x64.zip` (0.76 MB). |
| **Inno Setup Script Definition** | Verified | Defined at `packaging\innosetup\NexusDeviceService.iss`. |
| **ISCC Binary Compilation** | Deferred | Requires build agent with Inno Setup 6 installed (`iscc.exe`). |
| **EV Authenticode Code Signing** | Pending | Requires organization HSM certificate in release CI/CD. |
