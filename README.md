# Nexus Device Service Platform

Professional mobile-device and modem service desktop platform engineered with modern C#, WPF, and .NET 10 LTS.

## Architectural Highlights

- **Clean Architecture**: Strict separation of concerns with Domain, Device Abstractions, Application, Infrastructure, Presentation, and Test layers.
- **Dependency Inversion**: Dependency Injection via `Microsoft.Extensions.DependencyInjection` at the application composition root.
- **Modern MVVM**: Powered by `CommunityToolkit.Mvvm` (`ObservableObject`, `ObservableProperty`, `RelayCommand`).
- **Safety First**: Pre-flight capability verification prior to dispatching any operation to connected devices.
- **Central Package Management**: Version pins coordinated centrally via `Directory.Packages.props`.
- **Architectural Tests**: Automated xUnit assertions preventing invalid layer references.

---

## Projects in Solution

| Project | Type | Description |
|---|---|---|
| `NexusDeviceService.Domain` | Class Library (.NET 10) | Core entities, capability flags, operation status, and outcome contracts. |
| `NexusDeviceService.DeviceAbstractions` | Class Library (.NET 10) | Contracts for device connections, providers, sessions, and operations. |
| `NexusDeviceService.Application` | Class Library (.NET 10) | Orchestration services, session managers, and use-case workflows. |
| `NexusDeviceService.Infrastructure` | Class Library (.NET 10) | In-memory log buffer, system environment metrics, and DI extensions. |
| `NexusDeviceService.Desktop` | WPF Application (.NET 10 Windows) | Presentation shell, XAML views, design tokens, and composition root. |
| `NexusDeviceService.Tests` | xUnit Test Suite (.NET 10 Windows) | Architectural invariant, dependency injection, and capability gate tests. |

---

## Getting Started

### Prerequisites
- Windows 10/11
- .NET 10 SDK (`10.0.401` or later)

### Build
From the solution root directory:
```powershell
dotnet build
```

### Run Automated Tests
```powershell
dotnet test --logger "console;verbosity=normal"
```

### Run Desktop Application
```powershell
dotnet run --project src/NexusDeviceService.Desktop/NexusDeviceService.Desktop.csproj
```

---

### Release Packaging & Distribution
To build the Release artifacts and create a distributable portable zip:
```powershell
powershell -ExecutionPolicy Bypass -File scripts\publish-release.ps1
```
Output artifacts are generated in `publish\win-x64\` and `dist\`.

---

## Documentation
- [Architecture & Design Details](file:///docs/ARCHITECTURE.md)
- [Development & Troubleshooting Guide](file:///docs/DEVELOPMENT.md)
- [Release Notes (v1.0.0)](file:///docs/RELEASE_NOTES.md)
- [Packaging, Installer & Verification Guide](file:///docs/PACKAGING.md)
