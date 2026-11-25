# Builds tools\theme-editor.html from the template, embedding the Eclipse values
# (reference\scheme-0-eclipse.json) and every theme in UIThemes\themes\ as presets.
# Usage: .\tools\build-editor.ps1
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent

$eclipse = (Get-Content "$root\reference\scheme-0-eclipse.json" -Raw | ConvertFrom-Json).colors
$base = [ordered]@{}
$eclipse.PSObject.Properties | ForEach-Object { $base[$_.Name] = $_.Value }
# Static defaults outside ColorHelper (UserInterfaceController's static constructor).
$base['UserInterfaceController.HoverTipBackColorDefault'] = '#202020F8'
$base['UserInterfaceController.HoverTipTitleColorDefault'] = '#D4D4D4FF'
$base['UserInterfaceController.HoverTipTextColorDefault'] = '#D4D4D4FF'
# SelectionPanel's own colors (logged from the game).
$base['SelectionPanel.HotspotBackColor'] = '#404040E8'
$base['SelectionPanel.SectionBackColor'] = '#18181880'
# Dividers in the button strips: copies the containers make when created (ButtonColorText at that time,
# and a fixed #606060), and the sub-tab outline (AccentColor). See FINDINGS.
$base['EmpireSectionList.ForeColor'] = $base['ButtonColorText']
$base['AngledButtonStrip.ForeColor'] = $base['ButtonColorText']
$base['MapOverlayButtons.DarkColor'] = '#606060FF'
$base['SectionHeaderPanel.OutlineColor'] = $base['AccentColor']

$presets = [ordered]@{}
Get-ChildItem "$root\UIThemes\themes\*.json" | Sort-Object Name | ForEach-Object {
    # Strip // comments (whole-line and trailing) before parsing.
    $text = (Get-Content $_ -Raw) -replace '(?m)^\s*//.*$', '' -replace '(?m)\s//[^"\r\n]*$', ''
    $theme = $text | ConvertFrom-Json
    $colors = [ordered]@{}
    $theme.colors.PSObject.Properties | ForEach-Object { $colors[$_.Name] = $_.Value }
    $presets[$theme.name] = $colors
}

$data = "const BASE = " + ($base | ConvertTo-Json -Compress) + ";`nconst PRESETS = " + ($presets | ConvertTo-Json -Compress -Depth 4) + ";"
$html = (Get-Content "$PSScriptRoot\theme-editor.template.html" -Raw).Replace('/*__DATA__*/', $data)
Set-Content "$PSScriptRoot\theme-editor.html" $html -Encoding utf8NoBOM -NoNewline
# Same page for GitHub Pages (published from docs\ on master): https://xupisco.github.io/dw2-uithemes/
New-Item -ItemType Directory -Force "$root\docs" | Out-Null
Set-Content "$root\docs\index.html" $html -Encoding utf8NoBOM -NoNewline
Write-Host "Wrote tools\theme-editor.html and docs\index.html ($($base.Count) base colors; presets: $($presets.Keys -join ', '))"
