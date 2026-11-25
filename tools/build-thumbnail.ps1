# Renders tools\thumbnail.html to UIThemes\preview.jpg (2560x1440) with headless Edge.
# Usage: .\tools\build-thumbnail.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$edge = 'C:\Program Files (x86)\Microsoft\Edge\Application\msedge.exe'
$html = 'file:///' + ((Join-Path $PSScriptRoot 'thumbnail.html') -replace '\\', '/')
$png = Join-Path $env:TEMP 'dw2-uithemes-preview.png'
$jpg = Join-Path $root 'UIThemes\preview.jpg'

& $edge --headless=new --disable-gpu --hide-scrollbars --force-device-scale-factor=2 --window-size=1280,720 `
    --virtual-time-budget=2000 "--screenshot=$png" $html 2>$null | Out-Null
if (-not (Test-Path $png)) { throw 'Edge did not write the screenshot.' }

Add-Type -AssemblyName System.Drawing
$img = [System.Drawing.Image]::FromFile($png)
try {
    $codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
    $params = New-Object System.Drawing.Imaging.EncoderParameters 1
    $params.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 92L
    $img.Save($jpg, $codec, $params)
    Write-Host "Wrote UIThemes\preview.jpg ($($img.Width)x$($img.Height), $([math]::Round((Get-Item $jpg).Length / 1KB)) KB)"
} finally { $img.Dispose(); Remove-Item $png -ErrorAction SilentlyContinue }
