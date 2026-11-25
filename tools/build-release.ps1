# Builds the release files the DW2 Mod Launcher reads from GitHub (same layout as the Refreshed mods):
#   <id>-v<version>.zip             the package, inside a folder named after the mod
#   <id>-v<version>.zip.sha256      "<hash>  <zip name>"
#   <id>-v<version>.manifest.json   uid, version, tag, size, sha256, dependencies
#   <id>-v<version>.preview.jpg     the Workshop preview
# Output goes to release\ (not in git). Then: gh release create <tag> release\<tag>.* --target master
# Usage: .\tools\build-release.ps1 [-Project UIThemes]
param([string]$Project = 'UIThemes')
$ErrorActionPreference = 'Stop'
$root = Split-Path $PSScriptRoot -Parent
$projDir = Join-Path $root $Project
$mod = Get-Content (Join-Path $projDir 'mod.json') -Raw | ConvertFrom-Json
$launcher = Get-Content (Join-Path $projDir 'dw2modlauncher.json') -Raw | ConvertFrom-Json

dotnet build $projDir -c Release --nologo -v q
if ($LASTEXITCODE -ne 0) { throw 'Build failed.' }

$id = $mod.name
$tag = "$id-v$($mod.version)"
$outDir = Join-Path $root 'release'
$stage = Join-Path ([IO.Path]::GetTempPath()) "release-$tag"
Remove-Item $stage -Recurse -Force -ErrorAction SilentlyContinue
New-Item -ItemType Directory -Force $outDir, (Join-Path $stage $id) | Out-Null

# Same files install.ps1 copies into the game: the build output without debug symbols.
$build = Join-Path $projDir 'bin\Release\net8.0'
Get-ChildItem $build -Recurse -File | Where-Object Extension -ne '.pdb' | ForEach-Object {
    $dest = Join-Path (Join-Path $stage $id) $_.FullName.Substring($build.Length + 1)
    New-Item -ItemType Directory -Force (Split-Path $dest) | Out-Null
    Copy-Item $_.FullName $dest
}

$zip = Join-Path $outDir "$tag.zip"
Remove-Item $zip -ErrorAction SilentlyContinue
Add-Type -AssemblyName System.IO.Compression.FileSystem
# Forward slashes in entry names, as the launcher's zip (and other tools) expect.
$archive = [IO.Compression.ZipFile]::Open($zip, 'Create')
try {
    Get-ChildItem $stage -Recurse -File | ForEach-Object {
        $entry = $_.FullName.Substring($stage.Length + 1).Replace('\', '/')
        [IO.Compression.ZipFileExtensions]::CreateEntryFromFile($archive, $_.FullName, $entry, 'Optimal') | Out-Null
    }
} finally { $archive.Dispose() }
Remove-Item $stage -Recurse -Force

$size = (Get-Item $zip).Length
$sha = (Get-FileHash $zip -Algorithm SHA256).Hash.ToLower()
Set-Content (Join-Path $outDir "$tag.zip.sha256") "$sha  $tag.zip" -Encoding ascii

$manifest = [ordered]@{
    schema             = 1
    id                 = $id
    uid                = $launcher.uid
    name               = $id
    kinds              = @('code')
    version            = $mod.version
    tag                = $tag
    author             = $launcher.author
    shortDescription   = $mod.shortDescription
    minLauncherVersion = ''
    tags               = @()
    dependencies       = @()
    zip                = "$tag.zip"
    size               = $size
    sha256             = $sha
    publishedUtc       = (Get-Date).ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ")
}
if ($mod.workshopId) { $manifest.Insert(9, 'workshopId', [long]$mod.workshopId) }
$manifest | ConvertTo-Json -Depth 5 | Set-Content (Join-Path $outDir "$tag.manifest.json") -Encoding utf8NoBOM

Copy-Item (Join-Path $projDir $mod.previewImage) (Join-Path $outDir "$tag.preview.jpg") -Force

Write-Host "Release files for $tag in release\ ($([math]::Round($size / 1KB)) KB zip, sha256 $sha)"
