; ==============================================================================
; Nexus Device Service - Inno Setup Installer Script
; Version: 1.0.0
; Architecture: Windows x64 (.NET 10 LTS Desktop)
; ==============================================================================

#define MyAppName "Nexus Device Service"
#define MyAppVersion "1.0.0"
#define MyAppPublisher "Nexus Diagnostics Lab"
#define MyAppURL "https://github.com/nexus-diagnostics/nexus-device-service"
#define MyAppExeName "NexusDeviceService.Desktop.exe"
#define MyAppId "{{C74D2F28-5E24-4B71-92D9-22B1F2D065C8}}"

[Setup]
; Unique application GUID for clean upgrades and uninstall identification
AppId={#MyAppId}
AppName={#MyAppName}
AppVersion={#MyAppVersion}
AppVerName={#MyAppName} {#MyAppVersion}
AppPublisher={#MyAppPublisher}
AppPublisherURL={#MyAppURL}
AppSupportURL={#MyAppURL}/issues
AppUpdatesURL={#MyAppURL}/releases

; Default installation directory
DefaultDirName={autopf}\NexusDeviceService
DefaultGroupName={#MyAppName}
AllowNoIcons=yes

; Target architecture: 64-bit Windows 10/11
ArchitecturesAllowed=x64compatible
ArchitecturesInstallIn64BitMode=x64compatible

; Output configuration
OutputDir=..\..\dist
OutputBaseFilename=NexusDeviceService-Setup-1.0.0
SetupIconFile=..\..\src\NexusDeviceService.Desktop\Assets\app.ico
UninstallDisplayIcon={app}\{#MyAppExeName}

; Modern LZMA2 ultra compression
Compression=lzma2/ultra64
SolidCompression=yes
WizardStyle=modern

; Privilege handling: Allows per-user or administrative elevation
PrivilegesRequired=lowest
PrivilegesRequiredOverridesAllowed=dialog

; Code-signing hook placeholder for CI/CD signing:
; SignTool=signtool sign /sha1 <CertThumbprint> /tr http://timestamp.digicert.com /td sha256 /fd sha256 $f
; SignedUninstaller=yes

[Languages]
Name: "english"; MessagesFile: "compiler:Default.isl"

[Tasks]
Name: "desktopicon"; Description: "{cm:CreateDesktopIcon}"; GroupDescription: "{cm:AdditionalIcons}"; Flags: unchecked
Name: "startmenuicon"; Description: "Create Start Menu shortcut"; GroupDescription: "{cm:AdditionalIcons}"

[Files]
; Published binaries from win-x64 release build
Source: "..\..\publish\win-x64\*"; DestDir: "{app}"; Flags: ignoreversion recursesubdirs createallsubdirs
Source: "..\..\src\NexusDeviceService.Desktop\Assets\app.ico"; DestDir: "{app}\Assets"; Flags: ignoreversion

[Icons]
Name: "{autoprograms}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"; Tasks: startmenuicon
Name: "{autodesktop}\{#MyAppName}"; Filename: "{app}\{#MyAppExeName}"; IconFilename: "{app}\Assets\app.ico"; Tasks: desktopicon

[Run]
Filename: "{app}\{#MyAppExeName}"; Description: "{cm:LaunchProgram,{#StringChange(MyAppName, '&', '&&')}}"; Flags: nowait postinstall skipifsilent

[Code]
// User Data Preservation Policy:
// The application stores user preferences, session history, and redacted diagnostic logs in:
// %LocalAppData%\NexusDeviceService\
// During uninstall, application binaries in {app} are removed cleanly.
// User logs and preferences are preserved by default to protect historical diagnostics across upgrades.
procedure CurUninstallStepChanged(CurUninstallStep: TUninstallStep);
var
  DataPath: String;
begin
  if CurUninstallStep = usPostUninstall then
  begin
    DataPath := ExpandConstant('{localappdata}\NexusDeviceService');
    // If the user wishes to completely purge diagnostic traces, prompt or leave intact:
    if DirExists(DataPath) then
    begin
      if MsgBox('Do you want to delete local preference settings and diagnostic logs from ' + DataPath + '?', mbConfirmation, MB_YESNO) = IDYES then
      begin
        DelTree(DataPath, True, True, True);
      end;
    end;
  end;
end;
