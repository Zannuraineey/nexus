@echo off
title Nexus Device Service Launcher
set "DOTNET_ROOT=%LOCALAPPDATA%\Microsoft\dotnet"
set "DOTNET_ROOT_X64=%LOCALAPPDATA%\Microsoft\dotnet"
set "PATH=%LOCALAPPDATA%\Microsoft\dotnet;%PATH%"
echo Starting Nexus Device Service Desktop...
start "" "%~dp0src\NexusDeviceService.Desktop\bin\Release\net10.0-windows\NexusDeviceService.Desktop.exe"
