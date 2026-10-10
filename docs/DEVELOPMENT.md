# Nexus Device Service Platform — Development Guide

## Prerequisites

- **Operating System**: Windows 10 (1809+) or Windows 11
- **.NET SDK**: **.NET 10 LTS** (`10.0.401` or later)
- **Workloads**: `Microsoft.WindowsDesktop.App` (included with Windows .NET 10 SDK)
- **IDE**: Visual Studio 2026, VS Code with C# Dev Kit, or Antigravity IDE

## Solution Structure

```
NexusDeviceService/
├── .editorconfig                   # Coding style and naming rules
├── .gitignore                      # Git ignore patterns for build & test artifacts
├── Directory.Build.props           # Common compilation properties
├── Directory.Packages.props        # Central package version management (CPM)
├── NexusDeviceService.sln          # Master solution file
├── docs/                           # Architecture and development guides
├── src/
│   ├── NexusDeviceService.Domain
│   ├── NexusDeviceService.DeviceAbstractions
│   ├── NexusDeviceService.Application
│   ├── NexusDeviceService.Infrastructure
│   └── NexusDeviceService.Desktop
└── tests/
    └── NexusDeviceService.Tests
```

## Build & Test Instructions

### Restore and Build
```powershell
dotnet restore
dotnet build --configuration Release
```

### Run Automated Tests
```powershell
dotnet test --logger "console;verbosity=normal"
```

### Run Desktop Application
```powershell
dotnet run --project src/NexusDeviceService.Desktop/NexusDeviceService.Desktop.csproj
```

## Contribution & Architectural Rules

1. **Dependency Direction**:
   - `Domain` must NEVER reference `Desktop` or `Infrastructure`.
   - `DeviceAbstractions` must NOT depend on UI or persistence.
   - `Application` orchestrates use cases without UI-specific libraries.
   - Code-behind in WPF (`MainWindow.xaml.cs`) is reserved solely for presentation lifecycle and never performs business or hardware calls.
2. **Pre-flight Capability Gating**:
   - Never assume hardware capabilities. Always define capabilities in `DeviceCapability` and verify via `IOperationOrchestrator`.
3. **Simulation Mode**:
   - Simulated providers and virtual devices must always be explicitly labeled as simulation to prevent confusion with production hardware.

## UI Smoke-Test Verification Guidance

Execute this checklist when verifying the desktop application presentation:

1. **Dashboard (`DashboardView`)**:
   - Verify summary cards display device count and active interface port (`COM3 (Virtual)`).
   - Ensure `[SIMULATED]` indicators are visibly rendered across recent operations.
   - Resize window to minimum bounds (`980x640`) and verify no horizontal clipping or layout collapse occurs.
   - Click quick actions ("Devices", "Operations", "Console", "Settings") and verify instant routing.
2. **Devices (`DevicesView`)**:
   - Click "Scan Devices" — verify progress bar animates and 3 mock endpoints appear.
   - Select a device — verify model, chipset, port, and capability badges populate in details pane.
   - Test text filter (e.g. search "Snapdragon") and dropdown filter (e.g. "Virtual Simulation").
3. **Operations Center (`OperationsView`)**:
   - Verify available operations reflect target device's capability bitmask.
   - Select an operation and click "Execute Operation" — verify progress moves from 0% to 100%.
   - For operations with confirmation required, ensure execution is gated until checkbox is checked.
   - Test "Cancel" button on cancellable operations during execution.
   - Verify audit log table records operation timestamp, duration, and completion status.
4. **Console (`ConsoleView`)**:
   - Type in search box to filter by category or operation ID.
   - Change severity dropdown ("Warning", "Error", "All") and verify log filtering.
   - Click "Export (Text)" and "Export (JSON)" — verify file output and non-blocking notification.
   - Click "Clear Visible" — verify list clears without exceptions.
5. **Settings (`SettingsView`)**:
   - Switch Theme ("Dark" / "Light") — verify immediate theme dictionary swap without restart.
   - Adjust language, retention days, or default page, click "Save Platform Settings", restart app and verify values persist.

## Development & Environment Troubleshooting Guide

| Issue / Symptom | Root Cause | Recommended Remediation |
|---|---|---|
| `dotnet` command not found | .NET 10 SDK not in active session `PATH` | Prepend `C:\Users\lenovo\AppData\Local\Microsoft\dotnet` to session `$env:Path`. |
| `MSB3026: Could not copy Desktop.dll, file is locked by testhost` | Orphaned test host process from previous canceled run | Run `Get-Process -Name "testhost" -ErrorAction SilentlyContinue \| Stop-Process -Force`. |
| Circular DI resolution lock on startup | ViewModel constructor eagerly calling navigation service | Ensure ViewModels resolve navigation lazily or avoid resolving parent view models inside service constructors. |
| Tests fail with STA thread exception in WPF controls | WPF UI components accessed outside single-threaded apartment | Execute unit tests strictly against domain logic and ViewModels; mock navigation and Dispatcher where necessary. |
| Hardware operation shows "Capability Not Supported" | Pre-flight gating rejected operation | Inspect `device.SupportedCapabilities` against `operation.RequiredCapabilities`; ensure profile declares required bitmask. |

