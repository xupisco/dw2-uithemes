# Prepares the in-game captures for the Workshop gallery.
# Input: ss_<theme>.png in the project root (same scene, one per theme).
# Output, in workshop\:
#   <theme>.jpg      the full capture as JPEG
#   compare.jpg      the left panel of each theme side by side, with the theme name on top
# Usage: .\tools\build-screenshots.ps1 [-Crop 18,0,512,1079]   (x, y, width, height of the compared area)
param([int[]]$Crop = @(18, 0, 512, 1079))
$ErrorActionPreference = 'Stop'
Add-Type -AssemblyName System.Drawing
$root = Split-Path $PSScriptRoot -Parent
$outDir = Join-Path $root 'workshop'
New-Item -ItemType Directory -Force $outDir | Out-Null

$codec = [System.Drawing.Imaging.ImageCodecInfo]::GetImageEncoders() | Where-Object MimeType -eq 'image/jpeg'
$params = New-Object System.Drawing.Imaging.EncoderParameters 1
$params.Param[0] = New-Object System.Drawing.Imaging.EncoderParameter ([System.Drawing.Imaging.Encoder]::Quality), 92L

$files = Get-ChildItem $root -Filter 'ss_*.png' | Sort-Object Name
if (-not $files) { throw 'No ss_*.png in the project root.' }

$images = @()
try {
    foreach ($f in $files) {
        $name = $f.BaseName.Substring(3)
        $img = [System.Drawing.Image]::FromFile($f.FullName)
        $images += [pscustomobject]@{ Name = $name; Image = $img }
        $jpg = Join-Path $outDir "$name.jpg"
        $img.Save($jpg, $codec, $params)
        Write-Host "Wrote workshop\$name.jpg ($($img.Width)x$($img.Height))"
    }

    # Side-by-side comparison of the same area in every theme.
    # PowerShell names ignore case: $w and $W would be the same variable, hence $totalW/$totalH.
    $x, $y, $w, $h = $Crop
    $gap = 16; $pad = 24; $label = 56
    $totalW = $pad * 2 + $images.Count * $w + ($images.Count - 1) * $gap
    $totalH = $pad + $label + $h + $pad
    $bmp = New-Object System.Drawing.Bitmap $totalW, $totalH
    $g = [System.Drawing.Graphics]::FromImage($bmp)
    try {
        $g.Clear([System.Drawing.Color]::FromArgb(6, 7, 11))
        $g.TextRenderingHint = [System.Drawing.Text.TextRenderingHint]::AntiAliasGridFit
        $font = New-Object System.Drawing.Font 'Segoe UI Semibold', 22
        $brush = New-Object System.Drawing.SolidBrush ([System.Drawing.Color]::FromArgb(225, 228, 236))
        $fmt = New-Object System.Drawing.StringFormat
        $fmt.Alignment = 'Center'; $fmt.LineAlignment = 'Center'
        $src = New-Object System.Drawing.Rectangle $x, $y, $w, $h
        for ($i = 0; $i -lt $images.Count; $i++) {
            $left = $pad + $i * ($w + $gap)
            $title = $images[$i].Name.ToUpper()
            $g.DrawString($title, $font, $brush, (New-Object System.Drawing.RectangleF $left, $pad, $w, $label), $fmt)
            $part = $images[$i].Image.Clone($src, [System.Drawing.Imaging.PixelFormat]::Format32bppArgb)
            try { $g.DrawImage($part, [int]$left, [int]($pad + $label), [int]$w, [int]$h) } finally { $part.Dispose() }
        }
    } finally { $g.Dispose() }
    $cmp = Join-Path $outDir 'compare.jpg'
    $bmp.Save($cmp, $codec, $params)
    Write-Host "Wrote workshop\compare.jpg (${totalW}x$totalH)"
    $bmp.Dispose()
} finally { $images | ForEach-Object { $_.Image.Dispose() } }
