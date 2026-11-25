# Builds a mod and installs it into the game's mods folder.
# Usage: .\install.ps1 [-Project UIThemes] [-GameDir "..."] [-NoRegister]
param(
    [string]$Project = 'UIThemes',
    [string]$GameDir = 'D:\games\steam\steamapps\common\Distant Worlds 2',
    [switch]$NoRegister
)
$ErrorActionPreference = 'Stop'

$projDir = Join-Path $PSScriptRoot $Project
$manifest = Get-Content (Join-Path $projDir 'mod.json') -Raw | ConvertFrom-Json
$modName = $manifest.name

dotnet build $projDir -c Release -p:GameDir="$GameDir" --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$outDir = Join-Path $projDir 'bin\Release\net8.0'
$target = Join-Path $GameDir "mods\$modName"
New-Item -ItemType Directory -Force $target | Out-Null
# Everything the build put in the output folder (DLL, mod.json, settings schema, themes\), except debug symbols.
Get-ChildItem $outDir -Recurse -File | Where-Object Extension -ne '.pdb' | ForEach-Object {
    $dest = Join-Path $target $_.FullName.Substring($outDir.Length + 1)
    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
    Copy-Item $_.FullName $dest -Force
}
Write-Host "Copied $modName $($manifest.version) to $target"

if ($NoRegister) { return }

# The game loads the mods listed in mods.json; the launcher keeps a copy per profile (mods.<profile>.json).
$modsDir = Join-Path $GameDir 'mods'
$lists = @(Join-Path $modsDir 'mods.json')
$profileFile = Join-Path $modsDir 'currentProfile.txt'
if (Test-Path $profileFile) {
    $profileName = (Get-Content $profileFile -Raw).Trim()
    $profileList = Join-Path $modsDir "mods.$profileName.json"
    if (Test-Path $profileList) { $lists += $profileList }
}
$entry = "mods/$modName"
foreach ($list in $lists) {
    $json = Get-Content $list -Raw | ConvertFrom-Json
    if ($json.order -contains $entry) { Write-Host "Already listed in $(Split-Path $list -Leaf)"; continue }
    Copy-Item $list "$list.$(Get-Date -Format 'yyyyMMdd-HHmmss').uithemes_backup"
    $json.order = @($json.order) + $entry
    ($json | ConvertTo-Json -Compress -Depth 5) | Set-Content $list -NoNewline -Encoding utf8NoBOM
    Write-Host "Added $entry to $(Split-Path $list -Leaf) (backup saved next to it)"
}
