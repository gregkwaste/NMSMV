# Run from any directory: powershell -File .\build\release.ps1
[CmdletBinding()]
param(
    [string]$OutputDirectory
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$projectDirectory = Split-Path $PSScriptRoot -Parent
$project = Join-Path $projectDirectory 'WPFModelViewer.csproj'
if (-not $OutputDirectory) {
    $OutputDirectory = Join-Path $projectDirectory 'artifacts'
}
$OutputDirectory = [System.IO.Path]::GetFullPath($OutputDirectory)

# Util is the source of truth for release names; DEBUG is intentionally excluded.
$source = Get-Content (Join-Path $projectDirectory 'Util.cs') -Raw
$parts = foreach ($field in @('VersionMajor', 'VersionMedium', 'VersionMinor')) {
    $match = [regex]::Match($source, "\bint\s+$field\s*=\s*(\d+)\s*;")
    if (-not $match.Success) { throw "Could not read $field from Util.cs." }
    $match.Groups[1].Value
}
$prerelease = [regex]::Match($source, '\bbool\s+Prerelease\s*=\s*(true|false)\s*;')
if (-not $prerelease.Success) { throw 'Could not read Prerelease from Util.cs.' }
$version = $parts -join '.'
if ($prerelease.Groups[1].Value -eq 'true') { $version += '-pre' }
$name = "NMSMV_v${version}_win-x64"
$archive = Join-Path $OutputDirectory "$name.zip"
if (Test-Path $archive) { throw "Archive already exists: $archive. Move or remove it before packaging again." }

Get-Command dotnet -ErrorAction Stop | Out-Null
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
# Never package the regular bin folder: it can contain stale DLLs and user files.
$staging = Join-Path $OutputDirectory ('.staging-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $staging | Out-Null
try {
    & dotnet publish $project --configuration Release --runtime win-x64 --self-contained false --output $staging '-p:DebugType=None' '-p:DebugSymbols=false'
    if ($LASTEXITCODE -ne 0) { throw "dotnet publish failed (exit code $LASTEXITCODE)." }

    $required = @(
        'NMSMV.exe', 'NMSMV.dll', 'NMSMV.deps.json', 'NMSMV.runtimeconfig.json',
        'libMBIN.dll', 'Resources/default.dds', 'Resources/default_mask.dds',
        'Resources/droid.fnt', 'Resources/droid.png', 'Shaders',
        'packed_codebooks_aoTuV_603.bin'
    )
    foreach ($relativePath in $required) {
        if (-not (Test-Path (Join-Path $staging $relativePath))) {
            throw "Release is missing required content: $relativePath"
        }
    }

    # Retain publish's dependency graph, including native DLLs and runtime folders.
    # Costura controls embedded managed dependencies; do not guess a DLL allowlist.
    Compress-Archive -Path (Join-Path $staging '*') -DestinationPath $archive -CompressionLevel Optimal
    Write-Host "Created $archive"
    Write-Host 'Requires the .NET 10 Desktop Runtime (Windows x64).'
}
finally {
    # Only remove this invocation's uniquely named staging directory.
    Remove-Item -LiteralPath $staging -Recurse -Force
}
