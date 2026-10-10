# Nexus Device Service Platform — Architecture Specification

## 1. Overview

**Nexus Device Service Platform** is a professional desktop application engineered for mobile device and modem diagnostic, service, and maintenance workflows. It adheres strictly to Clean Architecture, Separation of Concerns, MVVM presentation patterns, and defensive pre-flight capability gating.

## 2. Layered Architecture & Dependency Inversion

```
  ┌──────────────────────────────────────────────────────────────┐
  │                 NexusDeviceService.Desktop                   │
  │     (WPF, XAML Views, CommunityToolkit.Mvvm, Composition Root)│
  └───────────────┬───────────────────────────────┬──────────────┘
                  │                               │
                  ▼                               ▼
  ┌───────────────────────────────┐ ┌────────────────────────────┐
  │  NexusDeviceService.Application│ │NexusDeviceService.         │
  │  (Use Cases, Orchestrator,    │ │Infrastructure             │
  │   Session Lifecycle)          │ │(Logging, Environment, DI) │
  └───────────────┬───────────────┘ └─────────────┬──────────────┘
                  │                               │
                  ▼                               ▼
  ┌──────────────────────────────────────────────┐
  │      NexusDeviceService.DeviceAbstractions   │
  │      (Device Providers, Operations, Sessions)│
  └───────────────────────┬──────────────────────┘
                          │
                          ▼
  ┌──────────────────────────────────────────────┐
  │            NexusDeviceService.Domain         │
  │      (Entities, Capability Flags, Results)   │
  └──────────────────────────────────────────────┘
```

### Layer Rules:
- **`NexusDeviceService.Domain`**: Pure domain entities (`DeviceIdentifier`, `DeviceProfile`), operation status (`OperationStatus`), capability flags (`DeviceCapability`), and outcome contracts (`IOperationResult`). No external UI or infrastructure references.
- **`NexusDeviceService.DeviceAbstractions`**: Shared abstraction contracts for physical/simulated device communication channels (`IDeviceConnection`), providers (`IDeviceProvider`), sessions (`IDeviceSession`), and executable operations (`IDeviceOperation`).
- **`NexusDeviceService.Application`**: Business orchestration (`IDeviceSessionManager`, `IOperationOrchestrator`), event args, and pre-flight capability enforcement.
- **`NexusDeviceService.Infrastructure`**: System telemetries (`ISystemEnvironmentProvider`), in-memory and console structured logging sinks (`IAppLogSink`, `InMemoryLogSink`), and dependency injection extension methods.
- **`NexusDeviceService.Desktop`**: WPF presentation layer, XAML views, design tokens, `MainViewModel`, and application composition root (`App.xaml.cs`).
- **`NexusDeviceService.Tests`**: Automated unit and architecture rule verification using xUnit.

## 3. Capability-Driven Execution Safety

Operations are never invoked blindly:
1. Every `IDeviceOperation` declares a `RequiredCapability` (e.g. `DeviceCapability.AuthorizedSimUnlock`, `DeviceCapability.AtCommandSession`).
2. Before any operation begins, `IOperationOrchestrator` performs pre-flight checks against the active device's `DeviceProfile.SupportedCapabilities`.
3. If the active profile lacks the declared capability or if the device connection is disconnected, the operation is rejected before dispatching any command packets.

## 4. Packaging and Dependency Management

The solution enforces **Central Package Management (CPM)** via `Directory.Packages.props`. All project dependencies use unified, pinned versions:
- `CommunityToolkit.Mvvm`: `8.4.2`
- `Microsoft.Extensions.DependencyInjection`: `10.0.12`
- `Microsoft.Extensions.Logging`: `10.0.12`
- `Microsoft.Extensions.Configuration`: `10.0.12`
- `xunit`: `2.9.3`
- `Microsoft.NET.Test.Sdk`: `17.14.1`

## 5. Architectural Invariant Tests

Automated reflection tests in `NexusDeviceService.Tests.ArchitectureTests` guarantee that no reverse or forbidden references are introduced across solution layers.

## 6. Reusable Visual Design System & Dynamic Theming

The desktop presentation adheres to a technician-oriented design system:
- **Theme Architecture**: Dark-first by default with seamless Light theme support. Live theme changes are managed by `IThemeService` swapping resource dictionaries in `Application.Current.Resources.MergedDictionaries` at runtime without restarting the application.
- **Theme Preference Abstraction**: User preference stored via `IThemePreferenceStore` in Application/Infrastructure.
- **Palette Tokens**: Deep charcoal slate (`#0E131B`) workspace, crisp technical borders (`#2D3748`), restrained blue accent (`#2563EB`), and high-contrast semantic indicators (Success, Warning, Error, Simulation).
- **Reusable Components**:
  - `Typography.xaml`: Segoe UI Variable font stack and monospace formatting for AT serial logs.
  - `Dimensions.xaml`: Uniform 8pt/4pt technician spacing grid, radii, and control heights.
  - `VectorIcons.xaml`: Resolution-independent SVG `StreamGeometry` vector icons.
  - `Buttons.xaml`, `Inputs.xaml`, `Cards.xaml`, `Badges.xaml`, `Progress.xaml`, `DataGrids.xaml`, `Tooltips.xaml`, `Navigation.xaml`, `Dialogs.xaml`.
  - `StateView.xaml`: Standardized empty, loading, and error state surfaces.

## 7. Application Shell & Modular Page Navigation

The main shell architecture implements a technician workstation layout:
- **Title Bar**: Reliable header with application chip logo, product branding, .NET 10 LTS indicator, simulation badge, live theme switch toggle, and technician account session placeholder (`Technician Lab Station (Offline)`).
- **Navigation Service**: `INavigationService` decoupled from Views, resolving destination ViewModels on demand from `IServiceProvider` and caching instances to prevent memory leaks and redundant subscriptions.
- **Eight Distinct Modular Pages**:
  1. `Dashboard` (`DashboardView` / `DashboardViewModel`): Overview of channels, completed operations, and platform health.
  2. `Devices` (`DevicesView` / `DevicesViewModel`): Hardware matrix, virtual port discovery, and channel inspection.
  3. `Operations` (`OperationsView` / `OperationsViewModel`): Operation catalog with capability badge prerequisites.
  4. `Firmware` (`FirmwareView` / `FirmwareViewModel`): Firmware image validation and cryptographic verification.
  5. `Console` (`ConsoleView` / `ConsoleViewModel`): Diagnostic activity console observing `IAppLogSink`.
  6. `History` (`HistoryView` / `HistoryViewModel`): Session audit history with empty state.
  7. `Settings` (`SettingsView` / `SettingsViewModel`): Preferences for theme, baud rate, and log verbosity.
  8. `About` (`AboutView` / `AboutViewModel`): System environment metadata and safety policy.
- **Collapsible Activity Console**: Bottom panel expandable on demand to monitor recent log events without leaving the current view.
- **Connection / Status Strip**: Persistent footer displaying active connection state, selected channel, and engine status.
- **Windows Resizing**: Standard window management preserved with minimum dimensions (`980x640`).

## 8. Device Discovery Architecture & Simulation Abstraction

Phase 5 introduces discovery and channel management:
- **`DeviceDescriptor` (Domain)**: Rich domain record capturing stable device identity (`DeviceIdentifier`), optional manufacturer, model, chipset family, hardware revision, firmware version, connection interface (`DeviceInterfaceType`: USB, Serial COM, ADB, Fastboot, Virtual Simulation), detection timestamp, provider ID, and capability bitmask (`DeviceCapability`). Properties are nullable to gracefully reflect partial hardware query results.
- **`IDeviceDiscoveryProvider` (DeviceAbstractions)**: Pluggable discovery contract implemented by physical bus probes or virtual simulation drivers. Providers declare whether they are virtual simulation sources (`IsSimulationProvider`).
- **`DeviceDiscoveryService` (Application)**: Coordinates all registered discovery providers with concurrency control (`SemaphoreSlim`), cancellation token propagation, individual provider failure resilience, and discovery change notifications (`DevicesDiscovered` event).
- **`SimulatedDeviceDiscoveryProvider` (Infrastructure)**: Virtual driver for offline development and automated testing returning realistic Qualcomm Snapdragon, ZTE Blade, and MediaTek profiles clearly stamped with `IsSimulation = true` and `VirtualSimulation` interface tags. Provides configurable delay and empty-mode toggle for testing zero-device states.
- **`DevicesViewModel` & `DevicesView` (Desktop)**:
  - Consumes only `IDeviceDiscoveryService` (zero direct reference to the simulation provider).
  - Handles zero devices (customized `StateView`), one device, and multiple devices.
  - Search filtering across model, chipset, port name, and serial number.
  - Dedicated interface filter dropdown ("All Interfaces", "Serial COM", "USB", "Virtual Simulation").
  - Hardware scan with loading progress bar, status feedback, error banner, and scan cancellation.
  - Device details pane with comprehensive identity breakdown, gated capability chips, and an interrogation audit trail history placeholder.

## 9. Modular Device Architecture & Operation Registry

Phase 6 implements a maintainable, pluggable module architecture:
- **`IDeviceModule` (DeviceAbstractions)**: Pluggable contract declaring stable module ID, display name, version, capability bitmask (`DeviceCapability`), upstream dependency module IDs (`RequiredDependencyModuleIds`), availability status (`ModuleAvailabilityStatus`), and manufacturer/model compatibility (`ModuleCompatibilityInfo`).
- **`OperationDescriptor` (Domain)**: Strongly-typed operation declaration specifying operation ID, display name, description, required capabilities, risk classification (`OperationRiskClassification`: Safe, Diagnostic, Service, Critical), device connection prerequisite, user confirmation requirement, and cancellation support.
- **`IOperationHandler` (DeviceAbstractions)**: Execution contract receiving `IOperationContext` (target connection, device descriptor, parameters, and cancellation token) and `IOperationProgressReporter`.
- **`IOperationRegistry` (DeviceAbstractions) & `OperationRegistry` (Application)**: Central thread-safe registry discovering modules via Dependency Injection. Enforces:
  - Duplicate module ID rejection with informative exceptions.
  - Upstream dependency validation, reporting missing dependencies as fatal issues and preventing dependent modules from offering operations.
  - Defensive capability gating: `GetOperationsForDevice(DeviceDescriptor)` filters operations by verifying the target device's capability bitmask.
- **`SimulatedModemModule` (Infrastructure)**: Demonstration module exposing safe, virtualized diagnostic operations (`op.sim.modem.read_info`, `op.sim.modem.at_query`, `op.sim.modem.sim_status`) that report progress and results without altering physical hardware.
