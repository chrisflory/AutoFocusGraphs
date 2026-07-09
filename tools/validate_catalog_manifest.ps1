# Validates the catalog-pr manifest using gather.js from nina.plugin.manifests
param(
    [string] $RefDir = (Join-Path (Split-Path $PSScriptRoot -Parent) ".nina-manifests-ref"),
    [string] $Version = "0.1.0.0",
    [string] $NinaVersionBand = "3.0.0"
)

$ErrorActionPreference = "Stop"
$repoRoot = Split-Path $PSScriptRoot -Parent
$catalogManifest = Join-Path $repoRoot "manifests\catalog-pr\manifests\a\AutoFocusGraphs\$NinaVersionBand\$Version\manifest.json"

if (-not (Test-Path $catalogManifest)) {
    & (Join-Path $PSScriptRoot "sync_catalog_manifest.ps1") -Version $Version -NinaVersionBand $NinaVersionBand
}

$manifestJson = Get-Content $catalogManifest -Raw | ConvertFrom-Json
$checksum = $manifestJson.Installer.Checksum.Trim().ToUpperInvariant()
if ($checksum -notmatch '^[A-F0-9]{64}$') {
    throw "Installer.Checksum must be exactly 64 hex characters (SHA256). Got length $($checksum.Length) in $catalogManifest"
}

if (-not (Test-Path $RefDir)) {
    Write-Host "Cloning nina.plugin.manifests into $RefDir ..."
    git clone --depth 1 https://github.com/isbeorn/nina.plugin.manifests.git $RefDir
} else {
    Write-Host "Using existing reference clone: $RefDir"
}

$targetDir = Join-Path $RefDir "manifests\a\AutoFocusGraphs\$NinaVersionBand\$Version"
New-Item -ItemType Directory -Force -Path $targetDir | Out-Null
Copy-Item $catalogManifest (Join-Path $targetDir "manifest.json") -Force

$node = Get-Command node -ErrorAction SilentlyContinue
if (-not $node) {
    Write-Warning "Node.js not found. Install with: winget install OpenJS.NodeJS.LTS"
    Write-Warning "Then re-run: .\tools\validate_catalog_manifest.ps1"
    Write-Host ""
    Write-Host "Manual checks passed:"
    Write-Host "  - Catalog manifest exists at $catalogManifest"
    Write-Host "  - Copied to validation path under $targetDir"
    exit 1
}

Push-Location $RefDir
try {
    if (-not (Test-Path "node_modules")) {
        Write-Host "Running npm install ..."
        npm install
    }
    Write-Host "Running node gather.js (full catalog scan) ..."
    $output = node gather.js 2>&1 | Out-String
    $autoFocusLines = ($output -split "`n") | Where-Object { $_ -match 'AutoFocusGraphs' }
    if ($autoFocusLines) {
        Write-Host ($autoFocusLines -join "`n")
    }
    if ($output -match 'done - total:') {
        Write-Host (($output -split "`n") | Where-Object { $_ -match 'done - total:' })
    }
    if ($output -match "INVALID MANIFEST!.*AutoFocusGraphs") {
        Write-Error "Validation failed for AutoFocusGraphs manifest."
    }
    if ($output -notmatch "Manifest valid at .*AutoFocusGraphs") {
        Write-Error "AutoFocusGraphs manifest was not reported as valid."
    }
    Write-Host "AutoFocusGraphs catalog manifest is valid."
}
finally {
    Pop-Location
}
