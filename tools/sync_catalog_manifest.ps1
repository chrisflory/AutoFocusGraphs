# Copies the canonical plugin-repo manifest into the catalog-pr tree
# used for isbeorn/nina.plugin.manifests pull requests.
param(
    [Parameter(Mandatory = $true)]
    [string] $Version,

    [string] $NinaVersionBand = "3.0.0"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$source = Join-Path $repoRoot "manifests\$Version\manifest.json"
$destDir = Join-Path $repoRoot "manifests\catalog-pr\manifests\a\AutoFocusGraphs\$NinaVersionBand\$Version"
$dest = Join-Path $destDir "manifest.json"

if (-not (Test-Path $source)) {
    throw "Source manifest not found: $source"
}

New-Item -ItemType Directory -Force -Path $destDir | Out-Null
Copy-Item $source $dest -Force

# Uppercase checksum to match common catalog convention (must stay 64 hex chars for SHA256)
$json = Get-Content $dest -Raw | ConvertFrom-Json
if ($json.Installer.Checksum) {
    $checksum = $json.Installer.Checksum.Trim().ToUpperInvariant()
    if ($checksum -notmatch '^[A-F0-9]{64}$') {
        throw "Installer.Checksum must be exactly 64 hex characters (SHA256). Got length $($checksum.Length)."
    }
    $json.Installer.Checksum = $checksum
}
if (-not $json.Descriptions.PSObject.Properties['ScreenshotURL']) {
    $json.Descriptions | Add-Member -NotePropertyName ScreenshotURL -NotePropertyValue ""
}
if (-not $json.Descriptions.PSObject.Properties['AltScreenshotURL']) {
    $json.Descriptions | Add-Member -NotePropertyName AltScreenshotURL -NotePropertyValue ""
}
$json | ConvertTo-Json -Depth 20 | Set-Content $dest -Encoding utf8

Write-Host "Synced catalog manifest to:"
Write-Host "  $dest"
