<#
.SYNOPSIS
    Automated Release Build and Packaging Script for Nexus Device Service.
.DESCRIPTION
    Builds the solution in Release configuration, runs automated tests,
    publishes the desktop application for win-x64, packages a portable distribution ZIP,
    and optionally invokes Inno Setup Compiler (ISCC.exe) if available.
.PARAMETER SelfContained
    Whether to produce a self-contained runtime bundle (default: false, framework-dependent).
#>

[CmdletBinding()]
param(
    [switch]$SelfContained = $false,
    [string]$Configuration = "Release",
    [string]$Version = "1.0.0"
)

$ErrorActionPreference = "Stop"

# Ensure local .NET 10 is prioritized
$dotnetPath = "C:\Users\lenovo\AppData\Local\Microsoft\dotnet"
if (Test-Path $dotnetPath) {
    $env:Path = "$dotnetPath;$env:Path"
}

$repoRoot = Resolve-Path (Join-Path $PSScriptRoot "..")
$projectPath = Join-Path $repoRoot "src\NexusDeviceService.Desktop\NexusDeviceService.Desktop.csproj"
$publishDir = Join-Path $repoRoot "publish\win-x64"
$distDir = Join-Path $repoRoot "dist"
$issPath = Join-Path $repoRoot "packaging\innosetup\NexusDeviceService.iss"

Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " Nexus Device Service - Release Packaging Pipeline v$Version" -ForegroundColor Cyan
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host "Repo Root:     $repoRoot"
Write-Host "Configuration: $Configuration"
Write-Host "SelfContained: $SelfContained"
Write-Host ""

# 1. Clean previous publish and dist output
if (Test-Path $publishDir) {
    Write-Host "[1/5] Cleaning existing publish directory..." -ForegroundColor Yellow
    Remove-Item -Path $publishDir -Recurse -Force
}
if (-not (Test-Path $distDir)) {
    New-Item -ItemType Directory -Force -Path $distDir | Out-Null
}

# 2. Run automated tests to guarantee zero regressions
Write-Host "[2/5] Running automated tests..." -ForegroundColor Yellow
& dotnet test "$repoRoot\NexusDeviceService.slnx" -c $Configuration --no-restore
if ($LASTEXITCODE -ne 0) {
    Write-Error "Test suite failed! Aborting release packaging."
}
Write-Host "Tests passed successfully." -ForegroundColor Green

# 3. Publish Windows Desktop Application
Write-Host "[3/5] Publishing Desktop application for win-x64..." -ForegroundColor Yellow
$publishArgs = @(
    "publish",
    $projectPath,
    "-c", $Configuration,
    "-r", "win-x64",
    "-o", $publishDir,
    "/p:Version=$Version"
)

if ($SelfContained) {
    $publishArgs += "--self-contained", "true"
} else {
    $publishArgs += "--self-contained", "false"
}

& dotnet @publishArgs
if ($LASTEXITCODE -ne 0) {
    Write-Error "dotnet publish failed!"
}
Write-Host "Publish succeeded: $publishDir" -ForegroundColor Green

# 4. Create Portable Distribution ZIP
Write-Host "[4/5] Creating portable distribution ZIP archive..." -ForegroundColor Yellow
$zipOutput = Join-Path $distDir "NexusDeviceService-$Version-win-x64.zip"
if (Test-Path $zipOutput) {
    Remove-Item $zipOutput -Force
}
Compress-Archive -Path "$publishDir\*" -DestinationPath $zipOutput -CompressionLevel Optimal
$zipItem = Get-Item $zipOutput
$zipHash = (Get-FileHash -Path $zipOutput -Algorithm SHA256).Hash
Write-Host "Created portable archive: $zipOutput ($([math]::Round($zipItem.Length / 1MB, 2)) MB)" -ForegroundColor Green
Write-Host "SHA256: $zipHash" -ForegroundColor Gray

# 5. Build Inno Setup Installer if compiler is present
Write-Host "[5/5] Checking for Inno Setup compiler (ISCC.exe)..." -ForegroundColor Yellow
$isccCandidates = @(
    (Get-Command iscc.exe -ErrorAction SilentlyContinue | Select-Object -ExpandProperty Source),
    "${env:ProgramFiles(x86)}\Inno Setup 6\ISCC.exe",
    "${env:ProgramFiles}\Inno Setup 6\ISCC.exe"
) | Where-Object { $_ -and (Test-Path $_) }

if ($isccCandidates.Count -gt 0) {
    $iscc = $isccCandidates[0]
    Write-Host "Found Inno Setup Compiler at: $iscc" -ForegroundColor Green
    & $iscc $issPath
    if ($LASTEXITCODE -eq 0) {
        $setupExe = Join-Path $distDir "NexusDeviceService-Setup-$Version.exe"
        if (Test-Path $setupExe) {
            $setupItem = Get-Item $setupExe
            $setupHash = (Get-FileHash -Path $setupExe -Algorithm SHA256).Hash
            Write-Host "Installer built successfully: $setupExe ($([math]::Round($setupItem.Length / 1MB, 2)) MB)" -ForegroundColor Green
            Write-Host "SHA256: $setupHash" -ForegroundColor Gray
        }
    } else {
        Write-Warning "Inno Setup compilation exited with code $LASTEXITCODE."
    }
} else {
    Write-Host "Inno Setup (ISCC.exe) not found on host. Installer script ready at: packaging\innosetup\NexusDeviceService.iss" -ForegroundColor Yellow
    Write-Host "To compile installer on a machine with Inno Setup, run:"
    Write-Host "  & 'C:\Program Files (x86)\Inno Setup 6\ISCC.exe' packaging\innosetup\NexusDeviceService.iss" -ForegroundColor Gray
}

Write-Host ""
Write-Host "=================================================================" -ForegroundColor Cyan
Write-Host " Packaging Complete!" -ForegroundColor Cyan
Write-Host " Artifacts directory: $distDir"
Write-Host "=================================================================" -ForegroundColor Cyan
