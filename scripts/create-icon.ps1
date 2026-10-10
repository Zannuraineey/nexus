Add-Type -AssemblyName System.Drawing

$outputPath = Join-Path $PSScriptRoot "..\src\NexusDeviceService.Desktop\Assets\app.ico"
$dir = Split-Path $outputPath
if (-not (Test-Path $dir)) {
    New-Item -ItemType Directory -Force -Path $dir | Out-Null
}

$bmp = New-Object System.Drawing.Bitmap 48, 48
$g = [System.Drawing.Graphics]::FromImage($bmp)
$g.SmoothingMode = [System.Drawing.Drawing2D.SmoothingMode]::AntiAlias

# Dark tech background
$brushBg = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(15, 23, 42))
$g.FillEllipse($brushBg, 2, 2, 44, 44)

# Cyan/Sky accent ring
$penBorder = New-Object System.Drawing.Pen ([System.Drawing.Color]::FromArgb(56, 189, 248), 2)
$g.DrawEllipse($penBorder, 2, 2, 44, 44)

# Crisp white bold 'N' glyph
$brushN = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(255, 255, 255))
$font = New-Object System.Drawing.Font ('Segoe UI', 20, [System.Drawing.FontStyle]::Bold)
$sf = New-Object System.Drawing.StringFormat
$sf.Alignment = [System.Drawing.StringAlignment]::Center
$sf.LineAlignment = [System.Drawing.StringAlignment]::Center
$rect = New-Object System.Drawing.RectangleF 0, 0, 48, 48
$g.DrawString('N', $font, $brushN, $rect, $sf)

$g.Dispose()

$hIcon = $bmp.GetHicon()
$icon = [System.Drawing.Icon]::FromHandle($hIcon)
$fs = New-Object System.IO.FileStream ($outputPath, [System.IO.FileMode]::Create)
$icon.Save($fs)
$fs.Close()
$bmp.Dispose()

Write-Host "Created application icon at: $outputPath"
